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

    /// <summary>
    /// MLACP-343. Hoi nguoc VNPay xem mot giao dich thuc te ra sao (vnp_Command=querydr), thay vi
    /// chi ngoi cho callback.
    ///
    /// <para>Truoc task nay he thong CHI biet ve thanh toan qua callback. Mot callback mat han —
    /// endpoint chet suot ca cua so retry, hoac VNPay bo cuoc — la tien khach da tra ma he thong
    /// danh Failed va huy ve, trong khi khong mot cho nao doc PaymentStatus.Failed.</para>
    ///
    /// <para>Cung so do chu ky nhu RefundAsync (gia tri tho noi bang dau |, khong url-encode) nhung
    /// THU TU TRUONG KHAC. Xem chu thich tai cho dung trong VnPayService.</para>
    ///
    /// <para>LUU Y: VNPay khoa merchant API tren tai khoan sandbox theo mac dinh — cung tinh trang
    /// da ghi cho RefundAsync, va doc lap voi chuyen code dung hay sai.</para>
    /// </summary>
    Task<VnPayTransactionQueryResult> QueryTransactionAsync(
        VnPayTransactionQuery query, CancellationToken ct = default);
}

public record VnPayTransactionQuery(
    string TxnRef,                  // vnp_TxnRef goc (Payment.OrderId)
    string OrderInfo,
    string? TransactionNo,          // vnp_TransactionNo goc neu biet — tuy chon
    DateTimeOffset TransactionDate, // thoi diem giao dich goc
    string IpAddress);

/// <param name="IsQueryAnswered">
/// Lenh truy van co chay duoc khong. <c>false</c> nghia la <b>khong biet</b> — khong duoc doc thanh
/// "giao dich that bai".
/// </param>
/// <param name="IsPaid">
/// Giao dich co that su da thanh toan thanh cong khong. Chi co nghia khi
/// <paramref name="IsQueryAnswered"/> la <c>true</c>.
/// </param>
public record VnPayTransactionQueryResult(
    bool IsQueryAnswered,
    bool IsPaid,
    string ResponseCode,
    string TransactionStatus,
    string Message,
    string? TransactionNo,
    decimal? Amount);

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
