namespace MusicLounge.Infrastructure.Settings;

public sealed class VnPaySettings
{
    public string TmnCode { get; init; } = string.Empty;
    public string HashSecret { get; init; } = string.Empty;
    public string PaymentUrl { get; init; } = "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html";
    public string Version { get; init; } = "2.1.0";
}
