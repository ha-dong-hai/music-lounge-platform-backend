namespace MusicLounge.Application.FnbOrders.DTOs;

public sealed record FnbOrderPaymentInitiationDto(
    int OrderId,
    string PaymentGatewayOrderId,
    decimal Amount,
    string PaymentUrl
);
