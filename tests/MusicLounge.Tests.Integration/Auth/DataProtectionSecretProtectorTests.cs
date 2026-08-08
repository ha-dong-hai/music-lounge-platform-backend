using System.Security.Cryptography;
using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using MusicLounge.Infrastructure.Services;

namespace MusicLounge.Tests.Integration.Auth;

/// <summary>
/// Backs ISecretProtector, which HangfireBackgroundJobService uses to keep raw password-reset
/// tokens / OTP codes out of Hangfire's plaintext, SQL Server-persisted job storage — see
/// SendPasswordResetEmailJob/SendEmailVerificationCodeJob. Hangfire never actually runs jobs in
/// this test host (ApiFactory deliberately omits AddHangfireServer), so this is the only place the
/// Protect/Unprotect round trip itself gets exercised.
/// </summary>
public sealed class DataProtectionSecretProtectorTests
{
    private static DataProtectionSecretProtector CreateProtector()
        => new(new EphemeralDataProtectionProvider());

    [Fact]
    public void ProtectThenUnprotect_ReturnsOriginalPlaintext()
    {
        var protector = CreateProtector();

        var ciphertext = protector.Protect("raw-password-reset-token");

        protector.Unprotect(ciphertext).Should().Be("raw-password-reset-token");
    }

    [Fact]
    public void Protect_OutputDoesNotContainThePlaintextSecret()
    {
        var protector = CreateProtector();

        var ciphertext = protector.Protect("super-secret-otp-123456");

        ciphertext.Should().NotContain("super-secret-otp-123456");
    }

    [Fact]
    public void Unprotect_TamperedCiphertext_Throws()
    {
        var protector = CreateProtector();
        var ciphertext = protector.Protect("raw-password-reset-token");

        // Flip a character near the start rather than the end — base64url's last character can
        // encode "don't care" padding bits that decode to the same byte regardless of which valid
        // character occupies that slot, which made an end-of-string flip flaky (sometimes silently
        // decoded to the same ciphertext bytes instead of tampering anything).
        var flipIndex = ciphertext.Length / 2;
        var tampered = ciphertext[..flipIndex]
            + (ciphertext[flipIndex] == 'A' ? 'B' : 'A')
            + ciphertext[(flipIndex + 1)..];

        var act = () => protector.Unprotect(tampered);

        act.Should().Throw<CryptographicException>();
    }
}
