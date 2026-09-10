using MediatR;
using Microsoft.Extensions.Options;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Common.Settings;
using MusicLounge.Application.FnbOrders.DTOs;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.Exceptions;

namespace MusicLounge.Application.FnbOrders.Commands.InitiateFnbOrderPayment;

internal sealed class InitiateFnbOrderPaymentCommandHandler
    : IRequestHandler<InitiateFnbOrderPaymentCommand, FnbOrderPaymentInitiationDto>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly IVnPayService _vnPay;
    private readonly IAsyncKeyedLock _lock;
    private readonly BusinessSettings _settings;

    public InitiateFnbOrderPaymentCommandHandler(
        IUnitOfWork uow, ICurrentUserService currentUser, IVnPayService vnPay, IAsyncKeyedLock @lock,
        IOptions<BusinessSettings> settings)
    {
        _uow = uow;
        _currentUser = currentUser;
        _vnPay = vnPay;
        _lock = @lock;
        _settings = settings.Value;
    }

    public async Task<FnbOrderPaymentInitiationDto> Handle(
        InitiateFnbOrderPaymentCommand request, CancellationToken ct)
    {
        await using var _ = await _lock.AcquireAsync(FnbOrderPayments.LockKey(request.OrderId), ct);

        var orderRepo = _uow.Repository<FnbOrder, int>();
        var order = await orderRepo.GetByIdAsync(request.OrderId, ct)
            ?? throw new NotFoundException(nameof(FnbOrder), request.OrderId);

        // Only the audience who placed the order can pay it online — staff-placed orders (walk-in
        // guest, no app account) have no AudienceUserId to match against and must be settled in
        // cash by Staff via PUT /fnb-orders/{id}/status instead.
        if (order.AudienceUserId != _currentUser.UserId)
            throw new ForbiddenException("Bạn không có quyền thanh toán đơn này.");

        if (order.Status == FnbOrderStatus.Cancelled)
            throw new DomainException("Đơn này đã bị huỷ, không thể thanh toán.");

        // MLACP-349: truoc day chi soi Status == Paid. Don tra truoc qua VNPay nay khong con nhay sang
        // Paid (bep van phai lam tiep), nen phai hoi bang payments — khong thi khach tra duoc lan hai.
        if (order.Status == FnbOrderStatus.Paid
            || await FnbOrderPayments.HasConfirmedPaymentAsync(_uow, order.Id, exceptPaymentId: null, ct))
            throw new ConflictException("Đơn này đã được thanh toán.");

        // Cho phep tao link moi khi link cu con han: VNPay cam dung lai vnp_TxnRef ("Khong duoc trung
        // lap trong ngay"), nen khong the "tiep tuc dung giao dich cu" nhu Stripe khuyen — chan o day
        // chi khien khach dong nham tab bi ket toi het han link. Cho hai link cung duoc tra la viec cua
        // IPN: no kiem trang thai DON, dung nhu tai lieu VNPay yeu cau.

        var orderId = $"FNB-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..40];

        // MLACP-349: KHONG doi order.PaymentMethod o day nua. Truoc day no bi dat Gateway ngay khi khach
        // bam "thanh toan" — truoc khi co dong nao — nen neu khach bo do roi tra tien mat, ban ghi tien
        // mat mang Method = Gateway ma khong co ma giao dich. Phuong thuc chi duoc ghi khi tien that su
        // ve: ProcessFnbOrderPayment (online) hoac UpdateFnbOrderStatus (tien mat).

        _uow.Repository<Payment, int>().Add(new Payment
        {
            OrderId = orderId,
            PayerId = order.AudienceUserId,
            GrossAmount = order.TotalAmount,
            Method = PaymentMethod.Gateway,
            Status = PaymentStatus.Pending,
            ReferenceType = FnbOrderPayments.ReferenceType,
            ReferenceId = order.Id.ToString(),
            CreatedAt = DateTimeOffset.UtcNow
        });

        await _uow.SaveChangesAsync(ct);

        var paymentUrl = _vnPay.CreatePaymentUrl(new VnPayPaymentRequest(
            OrderId: orderId,
            Amount: order.TotalAmount,
            OrderInfo: $"Thanh toan don F&B #{order.Id}",
            ReturnUrl: _settings.FnbOrderPaymentReturnUrl,
            IpAddress: request.ClientIpAddress));

        return new FnbOrderPaymentInitiationDto(order.Id, orderId, order.TotalAmount, paymentUrl);
    }
}
