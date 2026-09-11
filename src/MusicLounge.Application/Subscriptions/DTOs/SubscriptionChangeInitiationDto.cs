namespace MusicLounge.Application.Subscriptions.DTOs;

/// <summary>
/// MLACP-371 — khởi tạo thanh toán đổi gói, kèm ước tính phần gói cũ được quy đổi. Con số chốt lại lúc VNPay xác
/// nhận (tính theo thời điểm đó), nên có thể nhỏ hơn ước tính một chút.
/// </summary>
public sealed record SubscriptionChangeInitiationDto(
    int PaymentId,
    string OrderId,
    decimal Amount,
    string PaymentUrl,
    decimal CreditValue,
    decimal CreditDays,
    DateTimeOffset EstimatedExpiresAt);
