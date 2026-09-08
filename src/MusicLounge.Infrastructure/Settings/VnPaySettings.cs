namespace MusicLounge.Infrastructure.Settings;

public sealed class VnPaySettings
{
    public string TmnCode { get; init; } = string.Empty;
    public string HashSecret { get; init; } = string.Empty;
    public string PaymentUrl { get; init; } = "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html";
    // Merchant API (refund/querydr) — a different base than PaymentUrl above, POST JSON, not a
    // browser-redirect URL. VNPay restricts refund on sandbox by default; contact VNPay support
    // to enable it for the merchant account before this endpoint returns anything but an error.
    public string RefundApiUrl { get; init; } = "https://sandbox.vnpayment.vn/merchant_webapi/api/transaction";
    public string Version { get; init; } = "2.1.0";
}
