namespace MusicLounge.Application.Common.Interfaces;

public interface IVnPayService
{
    string CreatePaymentUrl(VnPayPaymentRequest request);
    VnPayCallbackResult VerifyCallback(IDictionary<string, string> queryParams);

    /// <summary>
    /// Calls VNPay's Merchant API (POST /merchant_webapi/api/transaction, vnp_Command=refund) to
    /// refund a previously-confirmed payment. Distinct signing scheme from CreatePaymentUrl/
    /// VerifyCallback (raw pipe-joined values, no URL-encoding) — see VNPay's official Payment
    /// Gateway Techspec 2.1.0, "Truy vấn & Hoàn tiền" section. NOTE: VNPay restricts refund by
    /// default on sandbox merchant accounts — VNPay support must enable it before this can be
    /// exercised against a real sandbox, independent of whether this code is correct.
    /// </summary>
    Task<VnPayRefundResult> RefundAsync(VnPayRefundRequest request, CancellationToken ct = default);
}

public record VnPayPaymentRequest(
    string OrderId,
    decimal Amount,
    string OrderInfo,
    string ReturnUrl,
    string IpAddress);

public record VnPayCallbackResult(
    bool IsSignatureValid,
    bool IsSuccess,
    string TransactionId,
    string ResponseCode,
    decimal Amount);

public record VnPayRefundRequest(
    string TxnRef,                 // original vnp_TxnRef (Payment.OrderId)
    decimal Amount,
    string OrderInfo,
    bool IsFullRefund,             // true => vnp_TransactionType=02, false => 03 (partial)
    string? TransactionNo,         // original vnp_TransactionNo (Payment.TransactionId), if known
    DateTimeOffset TransactionDate, // original payment's transaction time (Payment.PaidAt)
    string CreatedBy,
    string IpAddress);

public record VnPayRefundResult(
    bool IsSuccess,
    string ResponseCode,
    string Message,
    string? TransactionNo);
