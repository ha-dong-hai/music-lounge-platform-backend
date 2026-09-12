using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.Exceptions;

namespace MusicLounge.Application.Refunds.Commands.ProvideRefundPayoutAccount;

/// <summary>
/// MLACP-387. Người mua tự khai tài khoản nhận hoàn và đồng ý nhận bằng chuyển khoản, khi VNPay không còn hoàn được
/// giao dịch gốc.
///
/// <para>Luật BVQLNTD 2023 Điều 38 khoản 4: hoàn trả theo phương thức người tiêu dùng đã thanh toán, trừ khi họ đồng ý
/// phương thức khác. Trước task này (MLACP-384) Admin chuyển khoản tới tài khoản tự tìm — không có bằng chứng người mua
/// đồng ý, và dễ chuyển nhầm người. Chỉ nhận khi thật sự hết đường cũ: còn trong hạn thì tiền phải về đúng phương thức
/// đã trả, có đối soát với cổng thanh toán.</para>
/// </summary>
internal sealed class ProvideRefundPayoutAccountCommandHandler : IRequestHandler<ProvideRefundPayoutAccountCommand, Unit>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly ISystemConfigService _config;
    private readonly INotificationService _notifications;
    private readonly IAsyncKeyedLock _lock;

    public ProvideRefundPayoutAccountCommandHandler(
        IUnitOfWork uow, ICurrentUserService currentUser, ISystemConfigService config,
        INotificationService notifications, IAsyncKeyedLock @lock)
    {
        _uow = uow;
        _currentUser = currentUser;
        _config = config;
        _notifications = notifications;
        _lock = @lock;
    }

    public async Task<Unit> Handle(ProvideRefundPayoutAccountCommand request, CancellationToken ct)
    {
        // Cung khoa voi ProcessRefundRequest — Admin khong duyet chuyen khoan dung luc nguoi mua dang doi tai khoan.
        await using var _ = await _lock.AcquireAsync($"refund-request:{request.RefundRequestId}", ct);

        var refundRepo = _uow.Repository<RefundRequest, int>();
        var refund = await refundRepo.GetByIdAsync(request.RefundRequestId, ct)
            ?? throw new NotFoundException(nameof(RefundRequest), request.RefundRequestId);

        if (refund.RequestedBy != _currentUser.UserId)
            throw new ForbiddenException("Yêu cầu hoàn tiền này không phải của bạn.");

        if (refund.Status != RefundRequestStatus.Pending)
            throw new ConflictException("Yêu cầu hoàn tiền này đã được xử lý.");

        var payment = await _uow.Repository<Payment, int>().GetByIdAsync(refund.PaymentId, ct)
            ?? throw new NotFoundException(nameof(Payment), refund.PaymentId);

        var now = DateTimeOffset.UtcNow;
        var windowDays = await RefundGatewayWindow.WindowDaysAsync(_config, ct);
        if (!RefundGatewayWindow.IsClosed(payment, windowDays, now))
            throw new DomainException(payment.Method == PaymentMethod.Gateway
                ? "Giao dịch này vẫn hoàn được qua VNPay về đúng phương thức bạn đã thanh toán — không cần tài khoản nhận."
                : "Giao dịch này trả bằng tiền mặt tại quầy — phòng trà hoàn tiền mặt trực tiếp cho bạn.");

        refund.PayoutBankName = request.BankName.Trim();
        refund.PayoutAccountNumber = request.AccountNumber.Trim();
        refund.PayoutAccountHolder = request.AccountHolder.Trim();
        refund.PayoutConsentAt = now;
        refundRepo.Update(refund);

        // Admin la nguoi chuyen khoan — phai biet yeu cau nay da du dieu kien, khong phai tu di soi danh sach.
        var admins = await _uow.Repository<User, int>().FindAsync(u => u.Role == UserRole.Admin && u.IsActive, ct);
        foreach (var admin in admins)
            await _notifications.NotifyAsync(
                admin.Id,
                NotificationType.RefundSlaBreached,
                "Người mua đã khai tài khoản nhận hoàn",
                $"Yêu cầu hoàn tiền #{refund.Id} ({refund.AmountRequested:N0}đ): người mua đã đồng ý nhận hoàn bằng chuyển " +
                $"khoản vào {refund.PayoutBankName} {RefundGatewayWindow.Masked(refund.PayoutAccountNumber)}. Chuyển khoản " +
                "rồi duyệt yêu cầu kèm mã chuyển khoản.",
                referenceType: "refund_request",
                referenceId: refund.Id.ToString(),
                ct: ct);

        // Luu SAU khi gui thong bao — NotificationService chi Add vao change tracker.
        await _uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
