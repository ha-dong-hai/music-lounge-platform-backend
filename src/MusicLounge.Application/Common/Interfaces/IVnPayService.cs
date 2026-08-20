namespace MusicLounge.Application.Common.Interfaces;

public interface IVnPayService
{
    string CreatePaymentUrl(VnPayPaymentRequest request);
    VnPayCallbackResult VerifyCallback(IDictionary<string, string> queryParams);
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
