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
    // Used to default to a hardcoded "https://musiclounge.vn/..." production URL — a deployment
    // that forgot to configure these (Production.Local.json/env vars/secret manager) would silently
    // send real users to that URL instead of failing loudly, and any future domain change would
    // require a code change instead of a config one. Empty by default like the three above;
    // Program.cs fails fast at startup if any of these six are still unconfigured.
    public string PaymentSuccessUrl { get; init; } = string.Empty;
    public string PaymentFailedUrl { get; init; } = string.Empty;
    public string PasswordResetUrl { get; init; } = string.Empty;
}
