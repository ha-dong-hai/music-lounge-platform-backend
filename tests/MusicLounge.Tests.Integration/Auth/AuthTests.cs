using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MusicLounge.Infrastructure.Persistence;
using MusicLounge.Tests.Integration.Helpers;

namespace MusicLounge.Tests.Integration.Auth;

/// <summary>
/// W09 — Register / VerifyEmail / Login
/// POST /api/v1/auth/register
/// POST /api/v1/auth/verify-email
/// POST /api/v1/auth/resend-verification-code
/// POST /api/v1/auth/login
/// </summary>
[Collection("Integration")]
public sealed class AuthTests
{
    private readonly ApiFactory _factory;

    public AuthTests(ApiFactory factory) => _factory = factory;

    private static string UniqueEmail() => $"user-{Guid.NewGuid():N}@test.com";

    private static string HashToken(string rawToken)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));

    private async Task RegisterAsync(HttpClient client, string email, string password)
        => await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            Email = email, Password = password, FullName = "Test User", Phone = (string?)null, AcceptTerms = true
        });

    /// <summary>Đăng ký rồi đánh dấu email đã xác thực thẳng trong DB — dùng cho các test không
    /// tập trung vào bản thân luồng verify (Login, ForgotPassword, ResetPassword).</summary>
    private async Task RegisterAndVerifyAsync(HttpClient client, string email, string password)
    {
        await RegisterAsync(client, email, password);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = await db.Users.SingleAsync(u => u.Email == email);
        user.EmailVerifiedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
    }

    // ─── Register ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Register_NewEmail_Returns200WithVerificationPending()
    {
        var client = _factory.CreateClient();
        var email = UniqueEmail();

        var res = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            Email = email,
            Password = "P@ssword123",
            FullName = "Test User",
            Phone = (string?)null,
            AcceptTerms = true
        });

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await res.Content.ReadFromJsonAsync<RegisterResponse>();
        body!.Data.Email.Should().Be(email);
        body.Data.VerificationCodeExpiresAt.Should().BeAfter(DateTimeOffset.UtcNow);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = await db.Users.SingleAsync(u => u.Email == email);
        user.EmailVerifiedAt.Should().BeNull();
        user.EmailVerificationCodeHash.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Register_DuplicateEmail_Returns409()
    {
        var client = _factory.CreateClient();
        var email = UniqueEmail();
        var payload = new
        {
            Email = email,
            Password = "P@ssword123",
            FullName = "Test User",
            Phone = (string?)null,
            AcceptTerms = true
        };

        await client.PostAsJsonAsync("/api/v1/auth/register", payload);
        var res = await client.PostAsJsonAsync("/api/v1/auth/register", payload);

        res.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // ─── Email verification ─────────────────────────────────────────────────

    [Fact]
    public async Task VerifyEmail_CorrectCode_Returns200WithToken()
    {
        var client = _factory.CreateClient();
        var email = UniqueEmail();
        await RegisterAsync(client, email, "P@ssword123");

        const string rawCode = "123456";
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var user = await db.Users.SingleAsync(u => u.Email == email);
            user.EmailVerificationCodeHash = HashToken(rawCode);
            user.EmailVerificationCodeExpiresAt = DateTimeOffset.UtcNow.AddMinutes(10);
            await db.SaveChangesAsync();
        }

        var res = await client.PostAsJsonAsync("/api/v1/auth/verify-email",
            new { Email = email, Code = rawCode });

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await res.Content.ReadFromJsonAsync<AuthResponse>();
        body!.Data.Token.Split('.').Should().HaveCount(3, "the response should contain a well-formed JWT");

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var verifiedUser = await verifyDb.Users.SingleAsync(u => u.Email == email);
        verifiedUser.EmailVerifiedAt.Should().NotBeNull();
        verifiedUser.EmailVerificationCodeHash.Should().BeNull();
    }

    [Fact]
    public async Task VerifyEmail_WrongCode_Returns401()
    {
        var client = _factory.CreateClient();
        var email = UniqueEmail();
        await RegisterAsync(client, email, "P@ssword123");

        var res = await client.PostAsJsonAsync("/api/v1/auth/verify-email",
            new { Email = email, Code = "000000" });

        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task VerifyEmail_NonExistentEmailAndWrongCodeForRealEmail_ReturnIdenticalMessages()
    {
        // Enumeration resistance (OWASP ASVS V2.1), matching LoginCommandHandler's pattern: a
        // nonexistent email must be indistinguishable from a real email with a wrong code — not
        // just by status code, but by message content too (previously "Mã xác thực không đúng."
        // vs "Email hoặc mã xác thực không đúng." leaked which case was which regardless of timing).
        var client = _factory.CreateClient();
        var realEmail = UniqueEmail();
        await RegisterAsync(client, realEmail, "P@ssword123");

        var nonExistentRes = await client.PostAsJsonAsync("/api/v1/auth/verify-email",
            new { Email = UniqueEmail(), Code = "000000" });
        var wrongCodeRes = await client.PostAsJsonAsync("/api/v1/auth/verify-email",
            new { Email = realEmail, Code = "000000" });

        nonExistentRes.StatusCode.Should().Be(wrongCodeRes.StatusCode);
        var nonExistentBody = await nonExistentRes.Content.ReadAsStringAsync();
        var wrongCodeBody = await wrongCodeRes.Content.ReadAsStringAsync();
        nonExistentBody.Should().Be(wrongCodeBody);
    }

    [Fact]
    public async Task VerifyEmail_FifthConsecutiveWrongCode_LocksAccount()
    {
        // A 6-digit OTP only has 1,000,000 possible values — IAuthAttemptTracker's lockout is what
        // actually makes brute-forcing it impractical instead of just a few minutes of guessing.
        var client = _factory.CreateClient();
        var email = UniqueEmail();
        await RegisterAsync(client, email, "P@ssword123");

        HttpResponseMessage last = null!;
        for (var i = 0; i < 5; i++)
        {
            last = await client.PostAsJsonAsync("/api/v1/auth/verify-email",
                new { Email = email, Code = "000000" });
        }
        last.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var lockedOutRes = await client.PostAsJsonAsync("/api/v1/auth/verify-email",
            new { Email = email, Code = "000000" });

        lockedOutRes.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var body = await lockedOutRes.Content.ReadAsStringAsync();
        body.Should().Contain("khóa");
    }

    [Fact]
    public async Task VerifyEmail_ExpiredCode_Returns401()
    {
        var client = _factory.CreateClient();
        var email = UniqueEmail();
        await RegisterAsync(client, email, "P@ssword123");

        const string rawCode = "123456";
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var user = await db.Users.SingleAsync(u => u.Email == email);
            user.EmailVerificationCodeHash = HashToken(rawCode);
            user.EmailVerificationCodeExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1);
            await db.SaveChangesAsync();
        }

        var res = await client.PostAsJsonAsync("/api/v1/auth/verify-email",
            new { Email = email, Code = rawCode });

        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task VerifyEmail_AlreadyVerified_Returns409()
    {
        var client = _factory.CreateClient();
        var email = UniqueEmail();
        await RegisterAndVerifyAsync(client, email, "P@ssword123");

        var res = await client.PostAsJsonAsync("/api/v1/auth/verify-email",
            new { Email = email, Code = "123456" });

        res.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task ResendVerificationCode_ExistingUnverifiedEmail_Returns204AndRegeneratesCode()
    {
        var client = _factory.CreateClient();
        var email = UniqueEmail();
        await RegisterAsync(client, email, "P@ssword123");

        string hashBefore;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            hashBefore = (await db.Users.SingleAsync(u => u.Email == email)).EmailVerificationCodeHash!;
        }

        var res = await client.PostAsJsonAsync("/api/v1/auth/resend-verification-code", new { Email = email });
        res.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var scope2 = _factory.Services.CreateScope();
        var db2 = scope2.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var hashAfter = (await db2.Users.SingleAsync(u => u.Email == email)).EmailVerificationCodeHash;
        hashAfter.Should().NotBe(hashBefore);
    }

    [Fact]
    public async Task ResendVerificationCode_NonExistentEmail_StillReturns204()
    {
        var client = _factory.CreateClient();

        var res = await client.PostAsJsonAsync("/api/v1/auth/resend-verification-code",
            new { Email = UniqueEmail() });

        res.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task ResendVerificationCode_AlreadyVerifiedEmail_StillReturns204AndDoesNotRegenerate()
    {
        var client = _factory.CreateClient();
        var email = UniqueEmail();
        await RegisterAndVerifyAsync(client, email, "P@ssword123");

        string hashBefore;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            hashBefore = (await db.Users.SingleAsync(u => u.Email == email)).EmailVerificationCodeHash!;
        }

        var res = await client.PostAsJsonAsync("/api/v1/auth/resend-verification-code", new { Email = email });
        res.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var scope2 = _factory.Services.CreateScope();
        var db2 = scope2.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = await db2.Users.SingleAsync(u => u.Email == email);
        user.EmailVerificationCodeHash.Should().Be(hashBefore, "an already-verified account must not get a fresh code");
    }

    // ─── Login ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Login_UnverifiedAccount_Returns401()
    {
        var client = _factory.CreateClient();
        var email = UniqueEmail();
        await RegisterAsync(client, email, "P@ssword123");

        var res = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { Email = email, Password = "P@ssword123" });

        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_CorrectPassword_Returns200WithValidToken()
    {
        var client = _factory.CreateClient();
        var email = UniqueEmail();
        await RegisterAndVerifyAsync(client, email, "P@ssword123");

        var res = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            Email = email,
            Password = "P@ssword123"
        });

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await res.Content.ReadFromJsonAsync<AuthResponse>();
        body!.Data.Token.Split('.').Should().HaveCount(3);
    }

    [Fact]
    public async Task Login_WrongPassword_Returns401()
    {
        var client = _factory.CreateClient();
        var email = UniqueEmail();
        await RegisterAndVerifyAsync(client, email, "P@ssword123");

        var res = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            Email = email,
            Password = "WrongPassword!"
        });

        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_FifthConsecutiveWrongPassword_LocksAccount()
    {
        // IAuthAttemptTracker locks the account after 5 consecutive failures — the 6th attempt,
        // even with the CORRECT password, must still be rejected while the lockout is active.
        var client = _factory.CreateClient();
        var email = UniqueEmail();
        await RegisterAndVerifyAsync(client, email, "P@ssword123");

        HttpResponseMessage last = null!;
        for (var i = 0; i < 5; i++)
        {
            last = await client.PostAsJsonAsync("/api/v1/auth/login",
                new { Email = email, Password = "WrongPassword!" });
        }
        last.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var lockedOutRes = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { Email = email, Password = "P@ssword123" });

        lockedOutRes.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var body = await lockedOutRes.Content.ReadAsStringAsync();
        body.Should().Contain("khóa", "the lockout message must be distinguishable from a plain wrong-password message");
    }

    [Fact]
    public async Task Login_SuccessAfterFailures_ResetsFailureCounter()
    {
        var client = _factory.CreateClient();
        var email = UniqueEmail();
        await RegisterAndVerifyAsync(client, email, "P@ssword123");

        for (var i = 0; i < 3; i++)
        {
            await client.PostAsJsonAsync("/api/v1/auth/login",
                new { Email = email, Password = "WrongPassword!" });
        }

        var successRes = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { Email = email, Password = "P@ssword123" });
        successRes.StatusCode.Should().Be(HttpStatusCode.OK);

        // 2 more wrong attempts after the reset (would be attempts #4-5 of the original streak if
        // the counter hadn't been cleared) must NOT trigger a lockout.
        for (var i = 0; i < 2; i++)
        {
            await client.PostAsJsonAsync("/api/v1/auth/login",
                new { Email = email, Password = "WrongPassword!" });
        }

        var stillUnlockedRes = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { Email = email, Password = "P@ssword123" });
        stillUnlockedRes.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ─── Forgot / reset password ────────────────────────────────────────────

    [Fact]
    public async Task ForgotPassword_ExistingEmail_Returns204AndStoresHashedToken()
    {
        var client = _factory.CreateClient();
        var email = UniqueEmail();
        await RegisterAsync(client, email, "OldPassword123");

        var res = await client.PostAsJsonAsync("/api/v1/auth/forgot-password", new { Email = email });

        res.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = await db.Users.SingleAsync(u => u.Email == email);
        user.PasswordResetTokenHash.Should().NotBeNullOrEmpty();
        user.PasswordResetTokenExpiresAt.Should().NotBeNull();
        user.PasswordResetTokenExpiresAt!.Value.Should().BeAfter(DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task ForgotPassword_NonExistentEmail_StillReturns204()
    {
        // Khong duoc lo qua status code viec email co ton tai hay khong (chong account enumeration).
        var client = _factory.CreateClient();

        var res = await client.PostAsJsonAsync("/api/v1/auth/forgot-password", new { Email = UniqueEmail() });

        res.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task ResetPassword_ValidToken_ChangesPasswordAndInvalidatesOldOne()
    {
        var client = _factory.CreateClient();
        var email = UniqueEmail();
        await RegisterAsync(client, email, "OldPassword123");

        // Token thô không bao giờ trả về qua API (đúng thiết kế) — mô phỏng "biết token thật" bằng
        // cách tự chọn 1 token rồi ghi thẳng hash tương ứng vào DB, giống hệt những gì handler thật
        // sẽ làm khi gửi email.
        var rawToken = "test-token-" + Guid.NewGuid().ToString("N");
        Guid stampBefore;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var user = await db.Users.SingleAsync(u => u.Email == email);
            user.PasswordResetTokenHash = HashToken(rawToken);
            user.PasswordResetTokenExpiresAt = DateTimeOffset.UtcNow.AddMinutes(30);
            user.EmailVerifiedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
            stampBefore = user.SecurityStamp;
        }

        var resetRes = await client.PostAsJsonAsync("/api/v1/auth/reset-password",
            new { Token = rawToken, NewPassword = "NewPassword456" });
        resetRes.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var loginOld = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { Email = email, Password = "OldPassword123" });
        loginOld.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var loginNew = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { Email = email, Password = "NewPassword456" });
        loginNew.StatusCode.Should().Be(HttpStatusCode.OK);

        // Rotated so any JWT issued before the reset — e.g. one an attacker already stole — fails
        // the OnTokenValidated check (Program.cs) on its very next request instead of staying
        // valid until it naturally expires.
        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var verifiedUser = await verifyDb.Users.SingleAsync(u => u.Email == email);
        verifiedUser.SecurityStamp.Should().NotBe(stampBefore);
    }

    [Fact]
    public async Task ResetPassword_TokenReused_SecondAttemptReturns401()
    {
        var client = _factory.CreateClient();
        var email = UniqueEmail();
        await RegisterAsync(client, email, "OldPassword123");

        var rawToken = "test-token-" + Guid.NewGuid().ToString("N");
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var user = await db.Users.SingleAsync(u => u.Email == email);
            user.PasswordResetTokenHash = HashToken(rawToken);
            user.PasswordResetTokenExpiresAt = DateTimeOffset.UtcNow.AddMinutes(30);
            await db.SaveChangesAsync();
        }

        var first = await client.PostAsJsonAsync("/api/v1/auth/reset-password",
            new { Token = rawToken, NewPassword = "NewPassword456" });
        first.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var second = await client.PostAsJsonAsync("/api/v1/auth/reset-password",
            new { Token = rawToken, NewPassword = "AnotherPassword789" });
        second.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ResetPassword_InvalidToken_Returns401()
    {
        var client = _factory.CreateClient();

        var res = await client.PostAsJsonAsync("/api/v1/auth/reset-password",
            new { Token = "totally-invalid-token", NewPassword = "NewPassword456" });

        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ─── Refresh / Logout ───────────────────────────────────────────────────

    private async Task<AuthResultData> LoginAsync(HttpClient client, string email, string password)
    {
        var res = await client.PostAsJsonAsync("/api/v1/auth/login", new { Email = email, Password = password });
        var body = await res.Content.ReadFromJsonAsync<AuthResponse>();
        return body!.Data;
    }

    [Fact]
    public async Task Login_CorrectPassword_AlsoReturnsRefreshToken()
    {
        var client = _factory.CreateClient();
        var email = UniqueEmail();
        await RegisterAndVerifyAsync(client, email, "P@ssword123");

        var tokens = await LoginAsync(client, email, "P@ssword123");

        tokens.RefreshToken.Should().NotBeNullOrEmpty();
        tokens.RefreshToken!.Split('.').Should().HaveCount(3);
        tokens.RefreshTokenExpiresAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Refresh_ValidRefreshToken_ReturnsNewTokenPairForSameUser()
    {
        // Can't verify the new access token against a live [Authorize] endpoint here — this test
        // host's auth (ApiFactory registers ONLY TestAuthHandler as the default scheme) ignores real
        // Bearer headers entirely — so this checks the response shape/identity instead, same level
        // Login's own test checks at.
        var client = _factory.CreateClient();
        var email = UniqueEmail();
        await RegisterAndVerifyAsync(client, email, "P@ssword123");
        var tokens = await LoginAsync(client, email, "P@ssword123");

        var refreshRes = await client.PostAsJsonAsync("/api/v1/auth/refresh",
            new { RefreshToken = tokens.RefreshToken });
        refreshRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var newTokens = (await refreshRes.Content.ReadFromJsonAsync<AuthResponse>())!.Data;

        // Not asserting newTokens.Token != tokens.Token here — JWT `exp` only has second-level
        // precision and every other claim is identical, so a refresh happening within the same UTC
        // second as login legitimately produces a byte-identical token. That's not a bug.
        newTokens.Token.Split('.').Should().HaveCount(3);
        newTokens.RefreshToken.Should().NotBeNullOrEmpty();
        newTokens.UserId.Should().Be(tokens.UserId);
        newTokens.Email.Should().Be(email);
    }

    [Fact]
    public async Task Refresh_AccessTokenSentAsRefreshToken_Returns401()
    {
        // token_type=refresh check in JwtTokenService.ValidateRefreshToken — an access token is a
        // structurally valid, correctly-signed JWT too, so without this check it would be silently
        // accepted at /refresh, which is not what it's for.
        var client = _factory.CreateClient();
        var email = UniqueEmail();
        await RegisterAndVerifyAsync(client, email, "P@ssword123");
        var tokens = await LoginAsync(client, email, "P@ssword123");

        var res = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { RefreshToken = tokens.Token });

        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_GarbageToken_Returns401()
    {
        var client = _factory.CreateClient();

        var res = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { RefreshToken = "not-a-jwt" });

        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // NOTE on the two tests below: this test host's auth (ApiFactory registers ONLY TestAuthHandler
    // as the default scheme) does not validate a real "Authorization: Bearer <jwt>" header at all —
    // TestAuthHandler reads X-Test-User-Id instead and ignores Authorization entirely, so a genuine
    // Login-issued access token presented as a Bearer header gets a bare 401 from the auth
    // middleware itself, before ActiveUserBehavior ever runs. Logout calls below therefore go
    // through CreateAuthenticatedClient (the Test scheme) instead of the real token. /refresh is
    // unaffected by this, since it reads the refresh token from the request BODY, not the
    // Authorization header — so it still exercises the real IJwtTokenService validation path.

    [Fact]
    public async Task Logout_RotatesSecurityStamp_InvalidatingAnyPreviouslyIssuedToken()
    {
        // Proves the actual point of the feature at the level this harness can reach: a previously
        // -issued token's "sec_stamp" claim is compared against User.SecurityStamp on every request
        // (ActiveUserBehavior) and every refresh (RefreshTokenCommandHandler) — so once logout
        // rotates the stamp, any token minted before this moment stops matching. See
        // Logout_ThenReuseOldRefreshToken_Returns401 for the end-to-end proof using a real token.
        Guid stampBefore;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            stampBefore = await db.Users
                .Where(u => u.Id == SeedHelper.AudienceId).Select(u => u.SecurityStamp).SingleAsync();
        }

        var client = _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience");
        (await client.PostAsync("/api/v1/auth/logout", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var scope2 = _factory.Services.CreateScope();
        var db2 = scope2.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var stampAfterLogout = await db2.Users
            .Where(u => u.Id == SeedHelper.AudienceId).Select(u => u.SecurityStamp).SingleAsync();

        stampAfterLogout.Should().NotBe(stampBefore);
    }

    [Fact]
    public async Task Logout_ThenReuseOldRefreshToken_Returns401()
    {
        var client = _factory.CreateClient();
        var email = UniqueEmail();
        await RegisterAndVerifyAsync(client, email, "P@ssword123");
        var tokens = await LoginAsync(client, email, "P@ssword123");

        var logoutClient = _factory.CreateAuthenticatedClient(tokens.UserId, tokens.Role);
        (await logoutClient.PostAsync("/api/v1/auth/logout", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var refreshRes = await client.PostAsJsonAsync("/api/v1/auth/refresh",
            new { RefreshToken = tokens.RefreshToken });

        refreshRes.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_WithoutToken_Returns401()
    {
        var client = _factory.CreateClient();

        var res = await client.PostAsync("/api/v1/auth/logout", null);

        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private sealed record AuthResponse(bool Success, AuthResultData Data);

    private sealed record AuthResultData(
        string Token, DateTimeOffset ExpiresAt, int UserId, string Email, string FullName, string Role,
        string? RefreshToken = null, DateTimeOffset? RefreshTokenExpiresAt = null);

    private sealed record RegisterResponse(bool Success, RegisterResultData Data);

    private sealed record RegisterResultData(string Email, string FullName, DateTimeOffset VerificationCodeExpiresAt);
}
