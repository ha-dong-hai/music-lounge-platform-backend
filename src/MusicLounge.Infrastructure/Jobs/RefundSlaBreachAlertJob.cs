using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Hangfire;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Enums;
using MusicLounge.Infrastructure.Persistence;

namespace MusicLounge.Infrastructure.Jobs;

/// <summary>
/// Alerts Admins about refund requests left pending too long.
///
/// Complaints already had SLA tracking (ComplaintSlaBreachAlertJob); refunds — the step where the
/// buyer's money is actually meant to come back — had none at all. A RefundRequest sat Pending
/// indefinitely with nothing chasing it, and nothing telling the buyer when to expect an answer.
///
/// Two separate deadlines are watched, and they mean different things:
///
///   SLA breach (refund_sla_hours, default 72h = 3 business days) — an operational commitment.
///     Điều 31, Luật Bảo vệ quyền lợi người tiêu dùng 2023 requires a business to acknowledge a
///     consumer's request within 03 business days; Eventbrite holds organizers to 5 business days
///     to answer a refund request. 72h is the stricter of the two and is what the buyer is told.
///
///   Gateway window (vnpay_refund_window_days, default 90) — a hard technical wall. VNPay's
///     merchant terms cap a refund at 3 months from the transaction, after which the gateway
///     refuses it outright. A request that crosses this is not late any more, it is UNRECOVERABLE
///     through the normal path and needs a manual bank transfer. That is a materially worse
///     situation than an overdue one, so it is alerted separately and more loudly, with a warning
///     while the deadline is still approaching rather than only once it has passed.
/// </summary>
public sealed class RefundSlaBreachAlertJob
{
    private const int DefaultSlaHours = 72;
    private const int DefaultWindowDays = 90;

    /// <summary>Warn this long before the gateway window shuts, while it can still be acted on.</summary>
    private const int WindowWarningDays = 14;

    private readonly ApplicationDbContext _ctx;
    private readonly ISystemConfigService _config;
    private readonly INotificationService _notifications;
    private readonly ILogger<RefundSlaBreachAlertJob> _logger;

    public RefundSlaBreachAlertJob(
        ApplicationDbContext ctx, ISystemConfigService config,
        INotificationService notifications, ILogger<RefundSlaBreachAlertJob> logger)
    {
        _ctx = ctx;
        _config = config;
        _notifications = notifications;
        _logger = logger;
    }

    [DisableConcurrentExecution(timeoutInSeconds: 30)]
    public async Task ExecuteAsync(IJobCancellationToken cancellationToken)
    {
        var ct = cancellationToken.ShutdownToken;
        var now = DateTimeOffset.UtcNow;

        var slaHours = await _config.GetIntAsync(ConfigKeys.RefundSlaHours, DefaultSlaHours, ct);
        var windowDays = await _config.GetIntAsync(ConfigKeys.VnPayRefundWindowDays, DefaultWindowDays, ct);

        // MLACP-345: chay TRUOC phan Pending ben duoi, vi phan do return som khi khong co yeu cau nao
        // dang cho — ma yeu cau tien mat can nhac lai chinh la nhung yeu cau DA duoc duyet.
        await AlertCashNotHandedBackAsync(now, slaHours, ct);

        // Status server-side, dates client-side — the SQLite provider used in tests cannot translate
        // an enum equality combined with a DateTimeOffset comparison (documented across this folder).
        var pending = await _ctx.RefundRequests
            .Where(r => r.Status == RefundRequestStatus.Pending)
            .Select(r => new { r.Id, r.PaymentId, r.CreatedAt, r.AmountRequested })
            .ToListAsync(ct);
        if (pending.Count == 0) return;

        var paymentIds = pending.Select(p => p.PaymentId).Distinct().ToList();
        var paidAtByPayment = (await _ctx.Payments
                .Where(p => paymentIds.Contains(p.Id))
                .Select(p => new { p.Id, p.PaidAt, p.CreatedAt })
                .ToListAsync(ct))
            .ToDictionary(p => p.Id, p => p.PaidAt ?? p.CreatedAt);

        var overdue = pending
            .Where(r => new DateTimeOffset(r.CreatedAt, TimeSpan.Zero).AddHours(slaHours) <= now)
            .ToList();
        var windowClosing = pending
            .Where(r => paidAtByPayment.TryGetValue(r.PaymentId, out var paidAt)
                        && paidAt.AddDays(windowDays - WindowWarningDays) <= now)
            .ToList();

        if (overdue.Count == 0 && windowClosing.Count == 0) return;

        var admins = await _ctx.Users
            .Where(u => u.Role == UserRole.Admin && u.IsActive)
            .Select(u => u.Id)
            .ToListAsync(ct);
        if (admins.Count == 0)
        {
            _logger.LogError(
                "Refund SLA breached but there is no active Admin to notify — Overdue={Overdue} " +
                "WindowClosing={WindowClosing} at {At}",
                overdue.Count, windowClosing.Count, now);
            return;
        }

        foreach (var refund in overdue)
        {
            var hoursOverdue = (int)(now - new DateTimeOffset(refund.CreatedAt, TimeSpan.Zero).AddHours(slaHours)).TotalHours;
            foreach (var adminId in admins)
            {
                await _notifications.NotifyAsync(
                    adminId,
                    NotificationType.RefundSlaBreached,
                    "Quá hạn xử lý hoàn tiền",
                    $"Yêu cầu hoàn tiền #{refund.Id} ({refund.AmountRequested:N0}đ) đã quá hạn " +
                    $"{hoursOverdue}h so với cam kết {slaHours}h. Người mua đang chờ tiền về.",
                    referenceType: "refund_request",
                    referenceId: refund.Id.ToString(),
                    ct: ct);
            }
        }

        foreach (var refund in windowClosing)
        {
            var deadline = paidAtByPayment[refund.PaymentId].AddDays(windowDays);
            var daysLeft = (int)(deadline - now).TotalDays;
            var expired = daysLeft <= 0;

            _logger.LogError(
                "Refund approaching or past the VNPay refund window — RefundRequestId={RefundRequestId} " +
                "PaymentId={PaymentId} DaysLeft={DaysLeft} Deadline={Deadline}",
                refund.Id, refund.PaymentId, daysLeft, deadline);

            foreach (var adminId in admins)
            {
                await _notifications.NotifyAsync(
                    adminId,
                    NotificationType.RefundSlaBreached,
                    expired ? "Hết hạn hoàn tiền qua VNPay" : "Sắp hết hạn hoàn tiền qua VNPay",
                    expired
                        ? $"Yêu cầu hoàn tiền #{refund.Id} đã quá {windowDays} ngày kể từ giao dịch — " +
                          "VNPay không còn nhận lệnh hoàn. Phải chuyển khoản thủ công cho người mua."
                        : $"Yêu cầu hoàn tiền #{refund.Id} chỉ còn {daysLeft} ngày trước khi VNPay " +
                          "ngừng nhận lệnh hoàn. Sau mốc đó phải chuyển khoản thủ công.",
                    referenceType: "refund_request",
                    referenceId: refund.Id.ToString(),
                    ct: ct);
            }
        }

        await _ctx.SaveChangesAsync(ct);
    }

    /// <summary>
    /// MLACP-345. Yeu cau hoan cua ve ban tai quay da duoc duyet, nhung phong tra chua xac nhan da tra
    /// tien mat, va da qua <c>refund_sla_hours</c> tinh tu luc duyet.
    ///
    /// <para>Dung lai dung moc SLA cua moi yeu cau hoan chu khong dat so moi — cung mot cam ket voi
    /// khach (72h, 3 ngay lam viec cua Dieu 31), du tien di duong nao. Nhac ca chu phong tra (nguoi
    /// dang no) lan Admin (nguoi co the can thiep). Chong trung bang chinh dong thong bao da gui: job
    /// nay chay dinh ky va yeu cau do van nam nguyen cho cu cho toi khi co nguoi xac nhan.</para>
    /// </summary>
    private async Task AlertCashNotHandedBackAsync(DateTimeOffset now, int slaHours, CancellationToken ct)
    {
        // Loc trang thai phia server, so thoi gian phia client — cung gioi han provider SQLite.
        var approved = await _ctx.RefundRequests
            .Where(r => r.Status == RefundRequestStatus.Approved && r.CashHandedBackAt == null)
            .Select(r => new { r.Id, r.PaymentId, r.ResolvedAt, r.AmountApproved, r.AmountRequested })
            .ToListAsync(ct);

        var overdue = approved
            .Where(r => r.ResolvedAt.HasValue && r.ResolvedAt.Value.AddHours(slaHours) <= now)
            .ToList();
        if (overdue.Count == 0) return;

        var paymentIds = overdue.Select(r => r.PaymentId).Distinct().ToList();
        var cashPaymentIds = (await _ctx.Payments
                .Where(p => paymentIds.Contains(p.Id) && p.Method == PaymentMethod.Cash)
                .Select(p => p.Id)
                .ToListAsync(ct))
            .ToHashSet();

        var owed = overdue.Where(r => cashPaymentIds.Contains(r.PaymentId)).ToList();
        if (owed.Count == 0) return;

        var admins = await _ctx.Users
            .Where(u => u.Role == UserRole.Admin && u.IsActive)
            .Select(u => u.Id)
            .ToListAsync(ct);

        var notified = false;

        foreach (var refund in owed)
        {
            var ownerId = await _ctx.Tickets
                .Where(t => t.PaymentId == refund.PaymentId)
                .Select(t => (int?)t.Show.Lounge.OwnerId)
                .FirstOrDefaultAsync(ct);

            var amount = refund.AmountApproved ?? refund.AmountRequested;

            if (ownerId is int owner)
                notified |= await NotifyOnceAsync(
                    owner, NotificationType.RefundOwedByVenue, refund.Id,
                    "Chua xac nhan tra tien mat cho khach",
                    $"Yeu cau hoan #{refund.Id} ({amount:N0}d) da duoc duyet qua {slaHours}h ma phong tra " +
                    "chua xac nhan da tra tien mat cho khach. Khach van dang cho.",
                    ct);

            foreach (var adminId in admins)
                notified |= await NotifyOnceAsync(
                    adminId, NotificationType.RefundSlaBreached, refund.Id,
                    "Phong tra chua tra tien mat hoan cho khach",
                    $"Yeu cau hoan #{refund.Id} ({amount:N0}d, ve ban tai quay) da duoc duyet qua " +
                    $"{slaHours}h ma phong tra chua xac nhan da tra. Nen tang khong giu khoan nay nen " +
                    "khong tu hoan thay duoc — can lien he phong tra.",
                    ct);
        }

        if (notified) await _ctx.SaveChangesAsync(ct);
    }

    private async Task<bool> NotifyOnceAsync(
        int userId, NotificationType type, int refundId, string title, string body, CancellationToken ct)
    {
        var already = await _ctx.Notifications.AnyAsync(
            n => n.UserId == userId && n.Type == type
                 && n.ReferenceType == "cash_refund" && n.ReferenceId == refundId.ToString(), ct);
        if (already) return false;

        await _notifications.NotifyAsync(
            userId, type, title, body,
            referenceType: "cash_refund", referenceId: refundId.ToString(), ct: ct);
        return true;
    }
}
