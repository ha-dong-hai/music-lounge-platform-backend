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
            Email = email, Password = password, FullName = "Test User", Phone = (string?)null
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
            Phone = (string?)null
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
            Phone = (string?)null
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
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var user = await db.Users.SingleAsync(u => u.Email == email);
            user.PasswordResetTokenHash = HashToken(rawToken);
            user.PasswordResetTokenExpiresAt = DateTimeOffset.UtcNow.AddMinutes(30);
            user.EmailVerifiedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
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

    private sealed record AuthResponse(bool Success, AuthResultData Data);

    private sealed record AuthResultData(
        string Token, DateTimeOffset ExpiresAt, int UserId, string Email, string FullName, string Role);

    private sealed record RegisterResponse(bool Success, RegisterResultData Data);

    private sealed record RegisterResultData(string Email, string FullName, DateTimeOffset VerificationCodeExpiresAt);
}
