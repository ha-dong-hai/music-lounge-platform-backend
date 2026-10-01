namespace MusicLounge.Application.Subscriptions.DTOs;

public sealed record SubscriptionPaymentInitiationDto(
    Guid PaymentId,
    string OrderId,
    decimal Amount,
    string PaymentUrl);
