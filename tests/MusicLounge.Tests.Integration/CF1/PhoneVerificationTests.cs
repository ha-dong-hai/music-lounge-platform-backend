using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using MusicLounge.Domain.Entities;
using MusicLounge.Infrastructure.Persistence;
using MusicLounge.Tests.Integration.Helpers;

namespace MusicLounge.Tests.Integration.CF1;

/// <summary>
/// NĐ 147/2024 phone verification (governance gap #4 from the 2026-08-09 production-hardening
/// audit — User.PhoneVerified previously existed but no flow ever set it).
/// POST /api/v1/me/phone/verification-code | POST /api/v1/me/phone/verify
/// </summary>
[Collection("Integration")]
public sealed class PhoneVerificationTests
{
    private readonly ApiFactory _factory;

    public PhoneVerificationTests(ApiFactory factory) => _factory = factory;

    private static string HashCode(string code)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(code)));

    private async Task SetPhoneAsync(int userId, string? phone, bool verified = false)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = await db.Users.FindAsync(userId);
        user!.Phone = phone;
        user.PhoneVerified = verified;
        user.PhoneVerificationCodeHash = null;
        user.PhoneVerificationCodeExpiresAt = null;
        await db.SaveChangesAsync();
    }

    private async Task SeedPendingCodeAsync(int userId, string code)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = await db.Users.FindAsync(userId);
        user!.PhoneVerificationCodeHash = HashCode(code);
        user.PhoneVerificationCodeExpiresAt = DateTimeOffset.UtcNow.AddMinutes(10);
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task RequestVerification_NoPhoneSet_Returns422()
    {
        await SetPhoneAsync(SeedHelper.AudienceId, phone: null);
        var client = _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience");

        var res = await client.PostAsync("/api/v1/me/phone/verification-code", null);

        res.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task RequestVerification_WithPhoneSet_Returns204()
    {
        await SetPhoneAsync(SeedHelper.AudienceId, phone: "0901234567");
        var client = _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience");

        var res = await client.PostAsync("/api/v1/me/phone/verification-code", null);

        res.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    // ─── E1 resend cooldown (MLACP-272, audit-flagged 2026-09-07) ──────────────
    // Regression test: unlike ResendVerificationCodeCommandHandler (email), this handler had no
    // cooldown of its own — every call unconditionally sent a real SMS, budgeted only by the shared
    // per-IP [EnableRateLimiting("auth")] policy (not a per-account guard).

    [Fact]
    public async Task RequestVerification_CalledTwiceWithinCooldown_SecondReturns422()
    {
        await SetPhoneAsync(SeedHelper.AudienceId, phone: "0901234567");
        var client = _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience");

        var first = await client.PostAsync("/api/v1/me/phone/verification-code", null);
        first.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var second = await client.PostAsync("/api/v1/me/phone/verification-code", null);

        second.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "phải có cooldown giữa 2 lần yêu cầu gửi SMS liên tiếp, tránh SMS-bombing");
    }

    [Fact]
    public async Task RequestVerification_AfterCooldownExpires_Returns204()
    {
        await SetPhoneAsync(SeedHelper.AudienceId, phone: "0901234567");
        // Giả lập lần gửi trước đã cách đây 61s (cooldown 60s) — cooldown được suy ra từ
        // PhoneVerificationCodeExpiresAt (= lần gửi trước + CodeLifetime 10 phút), nên đặt
        // ExpiresAt = now - 10 phút + 61 giây để lastSentAt tương ứng = now - 61 giây.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var user = await db.Users.FindAsync(SeedHelper.AudienceId);
            user!.PhoneVerificationCodeHash = HashCode("111111");
            user.PhoneVerificationCodeExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-10).AddSeconds(61);
            await db.SaveChangesAsync();
        }
        var client = _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience");

        var res = await client.PostAsync("/api/v1/me/phone/verification-code", null);

        res.StatusCode.Should().Be(HttpStatusCode.NoContent,
            "sau khi hết cooldown, request gửi lại phải được chấp nhận bình thường");
    }

    [Fact]
    public async Task VerifyPhone_CorrectCode_SetsPhoneVerifiedTrue()
    {
        await SetPhoneAsync(SeedHelper.AudienceId, phone: "0901234567");
        await SeedPendingCodeAsync(SeedHelper.AudienceId, "123456");
        var client = _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience");

        var res = await client.PostAsJsonAsync("/api/v1/me/phone/verify", new { Code = "123456" });

        res.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = await db.Users.FindAsync(SeedHelper.AudienceId);
        user!.PhoneVerified.Should().BeTrue();
        user.PhoneVerificationCodeHash.Should().BeNull();
    }

    [Fact]
    public async Task VerifyPhone_WrongCode_Returns401AndDoesNotVerify()
    {
        await SetPhoneAsync(SeedHelper.AudienceId, phone: "0901234567");
        await SeedPendingCodeAsync(SeedHelper.AudienceId, "123456");
        var client = _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience");

        var res = await client.PostAsJsonAsync("/api/v1/me/phone/verify", new { Code = "000000" });

        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = await db.Users.FindAsync(SeedHelper.AudienceId);
        user!.PhoneVerified.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateProfile_ChangingPhone_ResetsPhoneVerified()
    {
        await SetPhoneAsync(SeedHelper.AudienceId, phone: "0901234567", verified: true);
        var client = _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience");

        var res = await client.PutAsJsonAsync("/api/v1/me/profile",
            new { FullName = "Audience", Phone = "0999999999", AvatarUrl = (string?)null });
        res.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = await db.Users.FindAsync(SeedHelper.AudienceId);
        user!.PhoneVerified.Should().BeFalse();
    }
}
