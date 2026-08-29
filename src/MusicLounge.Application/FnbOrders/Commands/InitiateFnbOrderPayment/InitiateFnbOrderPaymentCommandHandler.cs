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
    private readonly BusinessSettings _settings;

    public InitiateFnbOrderPaymentCommandHandler(
        IUnitOfWork uow, ICurrentUserService currentUser, IVnPayService vnPay,
        IOptions<BusinessSettings> settings)
    {
        _uow = uow;
        _currentUser = currentUser;
        _vnPay = vnPay;
        _settings = settings.Value;
    }

    public async Task<FnbOrderPaymentInitiationDto> Handle(
        InitiateFnbOrderPaymentCommand request, CancellationToken ct)
    {
        var orderRepo = _uow.Repository<FnbOrder, int>();
        var order = await orderRepo.GetByIdAsync(request.OrderId, ct)
            ?? throw new NotFoundException(nameof(FnbOrder), request.OrderId);

        // Only the audience who placed the order can pay it online — staff-placed orders (walk-in
        // guest, no app account) have no AudienceUserId to match against and must be settled in
        // cash by Staff via PUT /fnb-orders/{id}/status instead.
        if (order.AudienceUserId != _currentUser.UserId)
            throw new ForbiddenException("Bạn không có quyền thanh toán đơn này.");

        if (order.Status is FnbOrderStatus.Paid or FnbOrderStatus.Cancelled)
            throw new DomainException($"Đơn đang ở trạng thái '{order.Status}', không thể thanh toán.");

        var orderId = $"FNB-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..40];

        order.PaymentMethod = PaymentMethod.Gateway;
        orderRepo.Update(order);

        _uow.Repository<Payment, int>().Add(new Payment
        {
            OrderId = orderId,
            PayerId = order.AudienceUserId,
            GrossAmount = order.TotalAmount,
            Method = PaymentMethod.Gateway,
            Status = PaymentStatus.Pending,
            ReferenceType = "FnbOrder",
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
