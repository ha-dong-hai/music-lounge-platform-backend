namespace MusicLounge.Application.Subscriptions.DTOs;

public sealed record SubscriptionPaymentInitiationDto(
    int PaymentId,
    string OrderId,
    decimal Amount,
    string PaymentUrl);
