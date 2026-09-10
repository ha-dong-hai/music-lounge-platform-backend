using MusicLounge.Application.Common.Interfaces;

namespace MusicLounge.Tests.Integration.Fakes;

/// <summary>
/// Returns success when vnp_ResponseCode == "00", failure otherwise.
/// </summary>
public sealed class FakeVnPayService : IVnPayService
{
    public string CreatePaymentUrl(VnPayPaymentRequest request)
        => $"https://sandbox.vnpay.test/pay?orderId={request.OrderId}&amount={request.Amount}";

    public VnPayCallbackResult VerifyCallback(IDictionary<string, string> queryParams)
    {
        queryParams.TryGetValue("vnp_ResponseCode", out var code);
        queryParams.TryGetValue("vnp_TxnRef", out var txnRef);
        queryParams.TryGetValue("vnp_Amount", out var amountStr);
        decimal.TryParse(amountStr, out var amount);

        var success = code == "00";
        return new VnPayCallbackResult(true, success, txnRef ?? "", code ?? "99", amount / 100m);
    }

    /// <summary>
    /// MLACP-337: truoc day luon tra thanh cong bat ke dau vao, ke ca khi khong co ma giao dich.
    /// Khong cong thanh toan nao lam duoc dieu do — hoan tien phai tro toi mot giao dich co that.
    /// Fake de thanh cong o day che mat mot loi that: ve ban tai quay (tien mat, khong co
    /// TransactionId) di thang vao lenh goi VNPay va ket Pending vinh vien.
    /// </summary>
    /// <summary>
    /// MLACP-343. Cau tra loi cua VNPay cho tung TxnRef, do chinh bai kiem tra dat vao.
    ///
    /// <para>Mac dinh la <b>khong tra loi duoc</b> — dung nhu mot tai khoan sandbox bi khoa merchant
    /// API se hanh xu, va cung la trang thai giu nguyen hanh vi cu cua he thong.</para>
    /// </summary>
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, bool> Answers = new();

    public static void SayPaid(string txnRef) => Answers[txnRef] = true;
    public static void SayNotPaid(string txnRef) => Answers[txnRef] = false;
    public static void Forget(string txnRef) => Answers.TryRemove(txnRef, out _);

    public Task<VnPayTransactionQueryResult> QueryTransactionAsync(
        VnPayTransactionQuery query, CancellationToken ct = default)
    {
        if (!Answers.TryGetValue(query.TxnRef, out var paid))
            return Task.FromResult(new VnPayTransactionQueryResult(
                IsQueryAnswered: false, IsPaid: false, "", "", "Merchant API not available", null, null));

        // ResponseCode "00" nghia la LENH TRUY VAN chay duoc — dung ca khi giao dich that bai.
        // TransactionStatus moi la ket qua thanh toan.
        return Task.FromResult(new VnPayTransactionQueryResult(
            IsQueryAnswered: true,
            IsPaid: paid,
            ResponseCode: "00",
            TransactionStatus: paid ? "00" : "02",
            Message: "Success",
            TransactionNo: paid ? $"Q{query.TxnRef}"[..Math.Min(16, query.TxnRef.Length + 1)] : null,
            Amount: null));
    }

    public Task<VnPayRefundResult> RefundAsync(VnPayRefundRequest request, CancellationToken ct = default)
        => Task.FromResult(string.IsNullOrWhiteSpace(request.TransactionNo)
            ? new VnPayRefundResult(false, "91", "Khong tim thay giao dich yeu cau hoan tra", null)
            : new VnPayRefundResult(true, "00", "Confirm Success", request.TransactionNo));
}
