using Microsoft.EntityFrameworkCore;
using Hangfire;
using MusicLounge.Application.Common;
using MusicLounge.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;
using MusicLounge.Domain.Enums;
using MusicLounge.Infrastructure.Persistence;

namespace MusicLounge.Infrastructure.Jobs;

public sealed class CancelAbandonedPaymentsJob
{
    /// <summary>
    /// Mac dinh khi <c>system_config</c> chua co khoa — xem <see cref="ConfigKeys.PaymentAbandonMinutes"/>
    /// de biet vi sao con so nay phai lon hon cua so retry IPN cua VNPay.
    /// </summary>
    public const int DefaultAbandonMinutes = 60;

    private readonly ApplicationDbContext _ctx;
    private readonly ISystemConfigService _config;
    private readonly IVnPayService _vnPay;
    private readonly INotificationService _notifications;
    private readonly ILogger<CancelAbandonedPaymentsJob> _logger;

    public CancelAbandonedPaymentsJob(
        ApplicationDbContext ctx,
        ISystemConfigService config,
        IVnPayService vnPay,
        INotificationService notifications,
        ILogger<CancelAbandonedPaymentsJob> logger)
    {
        _ctx = ctx;
        _config = config;
        _vnPay = vnPay;
        _notifications = notifications;
        _logger = logger;
    }

    // MLACP-333. Cho nay truoc day cho 30 phut, dua tren mot comment ghi "VNPay retries the
    // callback for ~15 minutes". Con so 15 phut do SAI: tai lieu chinh chu cua VNPay ghi ro IPN
    // duoc goi lai toi da 10 lan, moi lan cach nhau 5 phut — lan cuoi co the roi vao khoang phut
    // thu 50. Nen bien an toan ma comment cu tuong la +15 phut thuc ra la -20 phut: tu phut 30 den
    // phut 50, VNPay VAN dang retry hop le trong khi ve da bi huy va thanh toan da bi danh Failed.
    // Mot xac nhan thanh cong den trong khoang do se bi coi la callback trung lap va bo di —
    // khach mat tien ma khong co ve, va khong ai tim ra duoc vi khong cho nao doc PaymentStatus.Failed.
    [DisableConcurrentExecution(timeoutInSeconds: 30)]
    public async Task ExecuteAsync(IJobCancellationToken cancellationToken)
    {
        var ct = cancellationToken.ShutdownToken;
        var abandonMinutes = await _config.GetIntAsync(
            ConfigKeys.PaymentAbandonMinutes, DefaultAbandonMinutes, ct);
        var cutoff = DateTimeOffset.UtcNow.AddMinutes(-abandonMinutes);

        // Combining the Status equality with the CreatedAt comparison in one Where doesn't
        // translate under the SQLite provider used in tests (same limitation documented
        // repeatedly elsewhere in this codebase, empirically confirmed here by a test that used
        // to throw) — filter by Status server-side, the date client-side.
        var stalePaymentIds = (await _ctx.Payments
                .Where(p => p.Status == PaymentStatus.Pending)
                .Select(p => new { p.Id, p.CreatedAt })
                .ToListAsync(ct))
            .Where(p => p.CreatedAt <= cutoff)
            .Select(p => p.Id)
            .ToList();

        if (stalePaymentIds.Count == 0) return;

        // MLACP-343. Truoc khi huy ve cua ai do, HOI NGUOC VNPay xem ho da tra tien chua.
        //
        // Truoc task nay he thong chi biet ve thanh toan qua callback. Mot callback mat han —
        // endpoint chet suot ca cua so retry, hoac VNPay bo cuoc — la tien khach da tra ma he thong
        // danh Failed va huy ve, trong khi khong mot cho nao doc PaymentStatus.Failed. Tien mat
        // khong dau vet.
        stalePaymentIds = await KeepOnlyUnpaidAsync(stalePaymentIds, ct);
        if (stalePaymentIds.Count == 0) return;

        await _ctx.Tickets
            .Where(t => t.PaymentId.HasValue
                && stalePaymentIds.Contains(t.PaymentId.Value)
                && t.Status == TicketStatus.Pending)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.Status, TicketStatus.Cancelled), ct);

        // A second empirically-confirmed SQLite provider limitation (distinct from the one above):
        // ExecuteUpdateAsync's SetProperty translator chokes on the implicit DateTimeOffset ->
        // DateTimeOffset? conversion needed to assign UtcNow into the nullable UpdatedAt column.
        // Falls back to fetch-then-mutate — the batch here is inherently small (payments stuck
        // >30 minutes), so losing the single-statement bulk UPDATE is not a real cost.
        var stalePayments = await _ctx.Payments
            .Where(p => stalePaymentIds.Contains(p.Id))
            .ToListAsync(ct);
        foreach (var payment in stalePayments)
        {
            payment.Status = PaymentStatus.Failed;
            payment.UpdatedAt = DateTimeOffset.UtcNow;
        }
        await _ctx.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Bo khoi danh sach sap huy nhung giao dich ma VNPay xac nhan la DA THANH TOAN.
    ///
    /// <para><b>Khong hoi duoc thi van huy nhu cu.</b> VNPay khoa merchant API tren tai khoan
    /// sandbox theo mac dinh, nen truy van co the that bai hoan toan. Neu khong hoi duoc ma cung
    /// khong huy thi job tro nen vo dung: cho ngoi bi giu vinh vien va nguoi mua that khong con ve
    /// de mua. Bao ve duoc khi API co san, va khong lam hong he thong khi khong co.</para>
    /// </summary>
    private async Task<List<int>> KeepOnlyUnpaidAsync(List<int> paymentIds, CancellationToken ct)
    {
        var payments = await _ctx.Payments
            .Where(p => paymentIds.Contains(p.Id))
            .ToListAsync(ct);

        var keep = new List<int>();
        List<Domain.Entities.User>? admins = null;

        foreach (var payment in payments)
        {
            // Chi hoi ve giao dich di qua cong thanh toan. Ve ban tai quay thu tien mat, VNPay
            // chua bao gio biet den chung — xem MLACP-337.
            if (payment.Method != PaymentMethod.Gateway)
            {
                keep.Add(payment.Id);
                continue;
            }

            var result = await _vnPay.QueryTransactionAsync(new VnPayTransactionQuery(
                TxnRef: payment.OrderId,
                OrderInfo: $"Doi soat giao dich #{payment.Id}",
                TransactionNo: payment.TransactionId,
                TransactionDate: payment.CreatedAt,
                IpAddress: "127.0.0.1"), ct);

            if (!result.IsQueryAnswered)
            {
                // KHONG BIET, khong phai "that bai" — nhung neu khong huy thi cho ngoi ket vinh vien.
                _logger.LogWarning(
                    "Khong doi soat duoc voi VNPay truoc khi huy thanh toan bo roi — PaymentId={PaymentId} " +
                    "TxnRef={TxnRef} LyDo={Message}. Van huy theo hanh vi cu.",
                    payment.Id, payment.OrderId, result.Message);
                keep.Add(payment.Id);
                continue;
            }

            if (!result.IsPaid)
            {
                keep.Add(payment.Id);
                continue;
            }

            // VNPay noi khach DA TRA TIEN. Huy ve luc nay la lay tien cua ho ma khong dua gi.
            _logger.LogError(
                "VNPay xac nhan giao dich DA THANH TOAN cho mot thanh toan sap bi huy — tien da thu ma " +
                "he thong chua ghi nhan. PaymentId={PaymentId} TxnRef={TxnRef} TransactionNo={TransactionNo} " +
                "SoTien={Amount} at {At}",
                payment.Id, payment.OrderId, result.TransactionNo, result.Amount, DateTimeOffset.UtcNow);

            admins ??= await _ctx.Users.Where(u => u.Role == UserRole.Admin).ToListAsync(ct);

            foreach (var admin in admins)
            {
                // Chong bao lai moi phut: job chay moi phut va thanh toan nay se van o Pending.
                var alreadyTold = await _ctx.Notifications.AnyAsync(
                    n => n.UserId == admin.Id
                         && n.Type == NotificationType.PaymentConfirmedAfterExpiry
                         && n.ReferenceType == "payment"
                         && n.ReferenceId == payment.Id.ToString(), ct);
                if (alreadyTold) continue;

                await _notifications.NotifyAsync(
                    admin.Id,
                    NotificationType.PaymentConfirmedAfterExpiry,
                    "Doi soat VNPay: giao dich da thanh toan nhung he thong chua ghi nhan",
                    $"Doi soat voi VNPay cho thay giao dich {payment.OrderId} DA duoc thanh toan, nhung " +
                    "he thong chua nhan duoc callback nen chua cap gi cho khach. Ve cua khach da duoc " +
                    "giu lai chua huy. Can doi chieu roi cap ve hoac hoan tien cho khach.",
                    referenceType: "payment",
                    referenceId: payment.Id.ToString(),
                    ct: ct);
            }
        }

        await _ctx.SaveChangesAsync(ct);
        return keep;
    }
}
