using Hangfire;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Refunds;
using MusicLounge.Application.Refunds.Commands.ProcessRefundRequest;
using MusicLounge.Domain.Enums;
using MusicLounge.Infrastructure.Persistence;

namespace MusicLounge.Infrastructure.Jobs;

/// <summary>
/// MLACP-348 — tự duyệt yêu cầu hoàn tiền đã quá hạn xử lý.
///
/// <para><b>Vì sao.</b> <c>RefundSlaBreachAlertJob</c> chỉ báo Admin. Nếu Admin không xử lý thì người
/// mua chờ vô hạn — trong khi kháng cáo quá hạn của phòng trà thì đã được tự chấp nhận
/// (<see cref="AutoApproveOverdueAppealsJob"/>). Im lặng của nền tảng đang được xử lý có lợi cho
/// phòng trà mà bất lợi cho khách.</para>
///
/// <para><b>Vì sao an toàn để tự duyệt.</b> Không một yêu cầu hoàn nào mang số tiền do người mua tự
/// điền: huỷ vé lấy theo chính sách phòng trà đã công bố (<c>TicketRefundPolicy</c>), còn mọi đường
/// do nền tảng ép (huỷ show, gỡ nội dung, khiếu nại, chưa từng lên sóng, bị cắt ngang) đều 100%. Bước
/// duyệt của Admin là một chốt kiểm, không phải chỗ quyết số tiền. Admin vẫn còn nguyên
/// <c>refund_auto_approve_grace_hours</c> sau cảnh báo SLA để từ chối một yêu cầu đáng ngờ.</para>
///
/// <para><b>Không chép lại logic hoàn tiền.</b> Job gửi đúng <see cref="ProcessRefundRequestCommand"/>
/// mà Admin gửi — gọi VNPay, đảo bút toán, co tranche quyết toán, thu hồi phần đã giải ngân, báo
/// người mua, báo phòng trà với vé tiền mặt. Một quy tắc tiền có hai bản sao thì sớm muộn cũng lệch
/// (bài học MLACP-335).</para>
///
/// <para><b>Không vượt được những gì Admin cũng không vượt được.</b> Quá hạn VNPay nhận lệnh hoàn,
/// VNPay từ chối, hay Admin vừa xử lý xong — handler đều từ chối, và yêu cầu nằm nguyên
/// <c>Pending</c> cho lần chạy sau hoặc cho cảnh báo riêng về hạn VNPay.</para>
/// </summary>
public sealed class AutoApproveOverdueRefundsJob
{
    internal const int DefaultSlaHours = 72;
    internal const int DefaultGraceHours = 24;

    /// <summary>
    /// VNPay đòi IP của máy khởi lệnh hoàn. Job không có request nào để lấy IP, nên dùng đúng giá trị
    /// dự phòng mà <c>AdminController.ProcessRefundRequest</c> đang dùng.
    /// </summary>
    private const string ServerIpAddress = "127.0.0.1";

    private readonly ApplicationDbContext _ctx;
    private readonly ISystemConfigService _config;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AutoApproveOverdueRefundsJob> _logger;

    public AutoApproveOverdueRefundsJob(
        ApplicationDbContext ctx,
        ISystemConfigService config,
        IServiceScopeFactory scopeFactory,
        ILogger<AutoApproveOverdueRefundsJob> logger)
    {
        _ctx = ctx;
        _config = config;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    [DisableConcurrentExecution(timeoutInSeconds: 300)]
    public async Task ExecuteAsync(IJobCancellationToken cancellationToken)
    {
        var ct = cancellationToken.ShutdownToken;
        var now = DateTimeOffset.UtcNow;

        var slaHours = await _config.GetIntAsync(ConfigKeys.RefundSlaHours, DefaultSlaHours, ct);
        var graceHours = await _config.GetIntAsync(
            ConfigKeys.RefundAutoApproveGraceHours, DefaultGraceHours, ct);
        var waitedHours = slaHours + graceHours;

        // Lọc trạng thái phía server, so thời gian phía client — provider SQLite dùng trong test không
        // dịch được phép so enum kèm DateTimeOffset trong cùng một truy vấn.
        var pending = await _ctx.RefundRequests
            .Where(r => r.Status == RefundRequestStatus.Pending)
            .Select(r => new { r.Id, r.PaymentId, r.CreatedAt })
            .ToListAsync(ct);

        // MLACP-387: qua han VNPay thi lenh hoan tu dong chac chan bi handler tu choi — truoc day job thu lai moi lan chay,
        // am tham, mai mai. Yeu cau nhu vay chi dong duoc bang chuyen khoan tay voi su dong y cua nguoi mua;
        // RefundSlaBreachAlertJob nhac nguoi mua va bao Admin.
        var windowDays = await RefundGatewayWindow.WindowDaysAsync(_config, ct);
        var paymentIds = pending.Select(r => r.PaymentId).Distinct().ToList();
        var gatewayClosed = (await _ctx.Payments
                .Where(p => paymentIds.Contains(p.Id))
                .Select(p => new { p.Id, p.Method, p.PaidAt, p.CreatedAt })
                .ToListAsync(ct))
            .Where(p => RefundGatewayWindow.IsClosed(p.Method, p.PaidAt ?? p.CreatedAt, windowDays, now))
            .Select(p => p.Id)
            .ToHashSet();

        var due = pending
            .Where(r => new DateTimeOffset(r.CreatedAt, TimeSpan.Zero).AddHours(waitedHours) <= now)
            .Where(r => !gatewayClosed.Contains(r.PaymentId))
            .OrderBy(r => r.Id)
            .Select(r => r.Id)
            .ToList();

        foreach (var refundId in due)
            await TryApproveAsync(refundId, slaHours, graceHours, ct);
    }

    private async Task TryApproveAsync(int refundId, int slaHours, int graceHours, CancellationToken ct)
    {
        // Mỗi yêu cầu một scope riêng, tức một DbContext riêng. Handler ghi ProcessedBy/ResolvedAt lên
        // thực thể TRƯỚC khi gọi VNPay; nếu VNPay từ chối thì TransactionBehavior rollback giao dịch,
        // nhưng thực thể đã sửa vẫn nằm trong change tracker. Dùng chung DbContext thì lần
        // SaveChanges của yêu cầu kế tiếp sẽ ghi luôn các trường đó xuống — một yêu cầu vẫn Pending
        // lại mang ResolvedAt như thể đã được xử lý.
        using var scope = _scopeFactory.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        try
        {
            await sender.Send(new ProcessRefundRequestCommand(
                refundId,
                "Approved",
                ApprovedAmount: null,
                ClientIpAddress: ServerIpAddress,
                ResolutionNote: $"Tự động duyệt — yêu cầu đã quá {slaHours}h cam kết xử lý và thêm " +
                                $"{graceHours}h ân hạn mà chưa được Admin xử lý.",
                AutoApproved: true), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Một yêu cầu hỏng không được chặn các yêu cầu còn lại. Yêu cầu này nằm nguyên Pending:
            // lần chạy sau thử lại, và cảnh báo hạn VNPay vẫn đang theo dõi nó.
            _logger.LogWarning(ex,
                "Tu duyet hoan tien that bai, yeu cau van Pending — RefundRequestId={RefundRequestId}",
                refundId);
            return;
        }

        _logger.LogWarning(
            "Da tu duyet yeu cau hoan tien qua han — RefundRequestId={RefundRequestId} " +
            "SlaHours={SlaHours} GraceHours={GraceHours}",
            refundId, slaHours, graceHours);

        // Admin phải biết hệ thống đã thay họ quyết — và quyết cái gì.
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var notifications = scope.ServiceProvider.GetRequiredService<INotificationService>();

        var admins = await db.Users
            .Where(u => u.Role == UserRole.Admin && u.IsActive)
            .Select(u => u.Id)
            .ToListAsync(ct);

        foreach (var adminId in admins)
        {
            await notifications.NotifyAsync(
                adminId,
                NotificationType.RefundSlaBreached,
                "Đã tự động duyệt hoàn tiền",
                $"Yêu cầu hoàn tiền #{refundId} đã được hệ thống tự duyệt vì chờ quá {slaHours}h cam " +
                $"kết và thêm {graceHours}h ân hạn mà chưa ai xử lý.",
                referenceType: "refund_request",
                referenceId: refundId.ToString(),
                ct: ct);
        }

        await db.SaveChangesAsync(ct);
    }
}
