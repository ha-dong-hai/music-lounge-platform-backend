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
}
