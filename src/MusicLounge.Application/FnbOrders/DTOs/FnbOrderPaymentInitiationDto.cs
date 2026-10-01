namespace MusicLounge.Application.FnbOrders.DTOs;

public sealed record FnbOrderPaymentInitiationDto(
    Guid OrderId,
    string PaymentGatewayOrderId,
    decimal Amount,
    string PaymentUrl
);
