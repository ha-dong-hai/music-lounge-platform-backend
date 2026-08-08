using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Options;
using MusicLounge.Infrastructure.Services;
using MusicLounge.Infrastructure.Settings;

namespace MusicLounge.Tests.Integration.CF3;

/// <summary>
/// master-backend-techlead review — VnPayService.VerifyCallback is the entire trust boundary for
/// "did a real bank payment happen" (feeds tickets, donations, subscriptions), but the whole test
/// suite exercises FakeVnPayService instead (by design — no real VNPay sandbox call in CI), so
/// nothing had ever actually tested the real signature verification. Also the specific reason this
/// test exists: the string.Equals comparison there was replaced with a constant-time comparison
/// (OWASP A04) — this proves that change didn't silently break correctness.
///
/// Signs test payloads by reproducing VNPay's callback algorithm locally (PHP-urlencode-style
/// escaping of !*'(), HMAC-SHA512, sorted keys) rather than reflecting into VnPayService's private
/// methods — the algorithm is small, stable, and documented in VnPayService's own comments as
/// confirmed live against a real sandbox callback.
/// </summary>
// No ApiFactory/DB access today, but marked into the shared collection anyway (xunit runs
// [Collection]-less classes in parallel with everything else by default) so a future edit adding
// either doesn't silently start racing the rest of the suite's shared SQLite connection.
[Collection("Integration")]
public sealed class VnPayServiceTests
{
    private const string HashSecret = "TEST-HASH-SECRET-NOT-REAL-1234567890";

    private readonly VnPayService _sut = new(Options.Create(new VnPaySettings
    {
        TmnCode = "TESTCODE",
        HashSecret = HashSecret,
        PaymentUrl = "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html",
        Version = "2.1.0"
    }));

    private static Dictionary<string, string> SignedCallback(
        IDictionary<string, string> fields, string hashSecret)
    {
        var signed = new Dictionary<string, string>(fields);
        var sorted = new SortedDictionary<string, string>(fields);
        var signData = string.Join("&", sorted.Select(kv => $"{PhpUrlEncode(kv.Key)}={PhpUrlEncode(kv.Value)}"));
        var keyBytes = Encoding.UTF8.GetBytes(hashSecret);
        var dataBytes = Encoding.UTF8.GetBytes(signData);
        using var hmac = new HMACSHA512(keyBytes);
        signed["vnp_SecureHash"] = Convert.ToHexString(hmac.ComputeHash(dataBytes)).ToLower();
        return signed;
    }

    private static string PhpUrlEncode(string value) =>
        System.Net.WebUtility.UrlEncode(value)
            .Replace("(", "%28").Replace(")", "%29").Replace("*", "%2A").Replace("!", "%21").Replace("'", "%27");

    private static Dictionary<string, string> BasePaidFields() => new()
    {
        ["vnp_Amount"] = "10000000",
        ["vnp_ResponseCode"] = "00",
        ["vnp_TransactionNo"] = "14123456",
        ["vnp_TxnRef"] = "ML-20260805-abcdef",
        ["vnp_OrderInfo"] = "MusicLounge ticket purchase - 1 ticket(s)",
    };

    [Fact]
    public void VerifyCallback_ValidSignatureAndSuccessCode_IsValidAndSuccessful()
    {
        var callback = SignedCallback(BasePaidFields(), HashSecret);

        var result = _sut.VerifyCallback(callback);

        result.IsSignatureValid.Should().BeTrue();
        result.IsSuccess.Should().BeTrue();
        result.Amount.Should().Be(100_000m, "vnp_Amount is VNPay's amount x100");
        result.TransactionId.Should().Be("14123456");
    }

    [Fact]
    public void VerifyCallback_ValidSignatureButFailureResponseCode_IsValidButNotSuccessful()
    {
        var fields = BasePaidFields();
        fields["vnp_ResponseCode"] = "24"; // VNPay's "customer cancelled" code
        var callback = SignedCallback(fields, HashSecret);

        var result = _sut.VerifyCallback(callback);

        result.IsSignatureValid.Should().BeTrue("the signature itself is genuine, VNPay is just reporting a decline");
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public void VerifyCallback_TamperedAfterSigning_IsRejected()
    {
        var callback = SignedCallback(BasePaidFields(), HashSecret);
        // Attacker (or a buggy proxy) changes the paid amount after the hash was computed.
        callback["vnp_Amount"] = "1"; // 0.01 VND instead of 100,000 VND

        var result = _sut.VerifyCallback(callback);

        result.IsSignatureValid.Should().BeFalse("the hash no longer matches the tampered payload");
    }

    [Fact]
    public void VerifyCallback_WrongHashSecret_IsRejected()
    {
        // Signed with a different secret than the one VnPayService is configured with — simulates
        // a forged callback from anyone who doesn't actually know the real VNPay HashSecret.
        var callback = SignedCallback(BasePaidFields(), "WRONG-SECRET-AN-ATTACKER-MIGHT-GUESS");

        var result = _sut.VerifyCallback(callback);

        result.IsSignatureValid.Should().BeFalse();
    }

    [Fact]
    public void VerifyCallback_UppercaseHash_IsStillAcceptedCaseInsensitively()
    {
        var callback = SignedCallback(BasePaidFields(), HashSecret);
        callback["vnp_SecureHash"] = callback["vnp_SecureHash"].ToUpperInvariant();

        var result = _sut.VerifyCallback(callback);

        result.IsSignatureValid.Should().BeTrue(
            "the constant-time comparison must preserve the original case-insensitive matching");
    }

    [Fact]
    public void VerifyCallback_MissingSecureHash_IsRejectedNotThrown()
    {
        var fields = BasePaidFields(); // no vnp_SecureHash key at all

        var act = () => _sut.VerifyCallback(fields);

        act.Should().NotThrow("a malformed/incomplete callback must fail closed, not crash with a 500");
        _sut.VerifyCallback(fields).IsSignatureValid.Should().BeFalse();
    }
}
