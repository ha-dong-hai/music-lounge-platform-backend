namespace MusicLounge.Application.Common.Settings;

// Environment-specific settings only (URLs differ per deploy target).
// Business parameters (rates, durations) live in the system_config table — D9.
public sealed class BusinessSettings
{
    // 3 luong (ve/donate/subscription) MOI luong co Payment record + callback handler RIENG
    // (xem PaymentsController/DonationsController/SubscriptionsController) — khong duoc dung
    // chung 1 ReturnUrl, neu khong VNPay se redirect ve dung 1 endpoint cho ca 3 loai giao dich,
    // khien 2 loai con lai luon that bai xu ly du VNPay da thu tien thanh cong.
    public string TicketPaymentReturnUrl { get; init; } = string.Empty;
    public string DonationPaymentReturnUrl { get; init; } = string.Empty;
    public string SubscriptionPaymentReturnUrl { get; init; } = string.Empty;
    public string PaymentSuccessUrl { get; init; } = "https://musiclounge.vn/payment/success";
    public string PaymentFailedUrl { get; init; } = "https://musiclounge.vn/payment/failed";
    public string PasswordResetUrl { get; init; } = "https://musiclounge.vn/reset-password";
}
