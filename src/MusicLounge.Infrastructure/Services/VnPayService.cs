using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Infrastructure.Settings;

namespace MusicLounge.Infrastructure.Services;

internal sealed class VnPayService : IVnPayService
{
    private readonly VnPaySettings _settings;

    public VnPayService(IOptions<VnPaySettings> options)
        => _settings = options.Value;

    public string CreatePaymentUrl(VnPayPaymentRequest request)
    {
        var now = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(7));

        var param = new SortedDictionary<string, string>
        {
            ["vnp_Version"]    = _settings.Version,
            ["vnp_Command"]    = "pay",
            ["vnp_TmnCode"]    = _settings.TmnCode,
            ["vnp_Amount"]     = ((long)(request.Amount * 100)).ToString(),
            ["vnp_CreateDate"] = now.ToString("yyyyMMddHHmmss"),
            ["vnp_CurrCode"]   = "VND",
            ["vnp_IpAddr"]     = request.IpAddress,
            ["vnp_Locale"]     = "vn",
            ["vnp_OrderInfo"]  = request.OrderInfo,
            ["vnp_OrderType"]  = "other",
            ["vnp_ReturnUrl"]  = request.ReturnUrl,
            ["vnp_TxnRef"]     = request.OrderId,
            ["vnp_ExpireDate"] = now.AddMinutes(15).ToString("yyyyMMddHHmmss"),
        };

        // Request-signing side: confirmed live that VNPay's own inbound signature check accepts
        // (and expects) plain WebUtility.UrlEncode with '(' ')' left as literal characters — adding
        // the extra PHP-style escaping here made VNPay itself reject the URL with "Sai chữ ký" before
        // the user ever reached the bank page. Do NOT switch this to PhpUrlEncode.
        var query = BuildQueryString(param);
        var signature = ComputeHmacSha512(_settings.HashSecret, query);

        return $"{_settings.PaymentUrl}?{query}&vnp_SecureHash={signature}";
    }

    public VnPayCallbackResult VerifyCallback(IDictionary<string, string> queryParams)
    {
        queryParams.TryGetValue("vnp_SecureHash", out var secureHash); secureHash ??= string.Empty;
        queryParams.TryGetValue("vnp_ResponseCode", out var responseCode); responseCode ??= string.Empty;
        queryParams.TryGetValue("vnp_TransactionNo", out var transactionId); transactionId ??= string.Empty;
        queryParams.TryGetValue("vnp_Amount", out var amountStr); amountStr ??= "0";

        var filtered = new SortedDictionary<string, string>(
            queryParams
                .Where(kv => kv.Key != "vnp_SecureHash" && kv.Key != "vnp_SecureHashType")
                .ToDictionary(kv => kv.Key, kv => kv.Value));

        // Callback-verification side uses a DIFFERENT encoder than the request side above — see
        // BuildCallbackSignData's comment for the live-verified reason this asymmetry is real,
        // not a mistake.
        var signData = BuildCallbackSignData(filtered);
        var computed = ComputeHmacSha512(_settings.HashSecret, signData);

        // Constant-time comparison (OWASP A04, Cryptographic Failures) — this string.Equals was
        // the actual byte-for-byte trust decision for "is this payment genuine or forged," on an
        // AllowAnonymous endpoint. A naive comparison short-circuits on the first mismatched
        // character, which a sufficiently patient network-timing attack can in principle exploit
        // to recover the correct hash one byte at a time; comparing password/signature hashes this
        // way is the same class of issue LoginCommandHandler already guards against for password
        // verification a few files over.
        var isValid = FixedTimeEqualsIgnoreCase(computed, secureHash);
        var isSuccess = isValid && responseCode == "00";
        var amount = decimal.TryParse(amountStr, out var a) ? a / 100 : 0m;

        return new VnPayCallbackResult(isValid, isSuccess, transactionId, responseCode, amount);
    }

    // VNPay's own reference implementation signs/builds the query using WebUtility.UrlEncode
    // (application/x-www-form-urlencoded — space -> '+'), NOT Uri.EscapeDataString (space -> '%20').
    // Using the wrong encoder produces a different byte string to hash whenever any signed value
    // (e.g. vnp_OrderInfo) contains a space, which VNPay's server rejects as "Invalid signature".
    // Confirmed live this is the correct encoder for the OUTBOUND request signature (see caller).
    private static string BuildQueryString(SortedDictionary<string, string> param)
        => string.Join("&", param.Select(kv =>
            $"{System.Net.WebUtility.UrlEncode(kv.Key)}={System.Net.WebUtility.UrlEncode(kv.Value)}"));

    // Confirmed live with a real VNPay sandbox callback (vnp_ResponseCode=00, real bank transaction):
    // manually recomputing HMAC-SHA512 over the plain WebUtility.UrlEncode'd query did NOT match the
    // vnp_SecureHash VNPay sent — every callback was silently treated as an invalid/forged signature.
    // Replacing just '(' ')' with %28/%29 in vnp_OrderInfo ("... ticket(s)") reproduced VNPay's hash
    // byte-for-byte. VNPay's callback signer evidently escapes !*'() the way PHP's urlencode() does,
    // which .NET's WebUtility.UrlEncode does not — this is a DIFFERENT algorithm than the request side
    // uses (confirmed separately: applying this same escaping to CreatePaymentUrl made VNPay reject the
    // request itself with "Sai chữ ký" before reaching the bank page). Keep these two encoders separate.
    private static string BuildCallbackSignData(SortedDictionary<string, string> param)
        => string.Join("&", param.Select(kv =>
            $"{PhpUrlEncode(kv.Key)}={PhpUrlEncode(kv.Value)}"));

    private static string PhpUrlEncode(string value) =>
        System.Net.WebUtility.UrlEncode(value)
            .Replace("(", "%28")
            .Replace(")", "%29")
            .Replace("*", "%2A")
            .Replace("!", "%21")
            .Replace("'", "%27");

    private static bool FixedTimeEqualsIgnoreCase(string a, string b)
    {
        // Hash hex length is fixed/public (SHA512 -> 128 hex chars), so comparing lengths first
        // leaks nothing an attacker doesn't already know; CryptographicOperations.FixedTimeEquals
        // requires equal-length spans anyway.
        if (a.Length != b.Length) return false;
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(a.ToUpperInvariant()),
            Encoding.UTF8.GetBytes(b.ToUpperInvariant()));
    }

    private static string ComputeHmacSha512(string key, string data)
    {
        var keyBytes  = Encoding.UTF8.GetBytes(key);
        var dataBytes = Encoding.UTF8.GetBytes(data);
        using var hmac = new HMACSHA512(keyBytes);
        return Convert.ToHexString(hmac.ComputeHash(dataBytes)).ToLower();
    }
}
