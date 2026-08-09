using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Infrastructure.Persistence;
using MusicLounge.Tests.Integration.Helpers;

namespace MusicLounge.Tests.Integration.Users;

/// <summary>
/// DSAR erasure (Luật 91/2025/QH15 + NĐ 356/2025/NĐ-CP) — governance gap #1 (erasure half) from
/// the 2026-08-09 production-hardening audit. Distinct from DELETE /me (DeactivateMyAccount, a
/// reversible flag flip): this scrubs identifying fields in place and is irreversible.
///
/// Every test here uses a freshly-created, disposable account — never SeedHelper.AudienceId or
/// any other shared seed fixture, since [Collection("Integration")] means every test class in this
/// suite shares one database and erasure (IsActive=false, PII scrubbed, preference rows deleted)
/// would corrupt whatever runs afterward using that shared account.
/// POST /api/v1/me/data-erasure
/// </summary>
[Collection("Integration")]
public sealed class DataErasureTests
{
    private readonly ApiFactory _factory;

    public DataErasureTests(ApiFactory factory) => _factory = factory;

    private static string UniqueEmail() => $"erase-{Guid.NewGuid():N}@test.com";

    /// <summary>Registers a real local account (real hashed password via the actual Register flow) and returns its Id.</summary>
    private async Task<int> CreateLocalAccountAsync(string email, string password)
    {
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            Email = email, Password = password, FullName = "Erase Me", Phone = (string?)null
        });

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = await db.Users.SingleAsync(u => u.Email == email);
        return user.Id;
    }

    /// <summary>Directly inserts a Google-only account (no password) — mirrors AuthProvider="google" shape.</summary>
    private async Task<int> CreateGoogleOnlyAccountAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = new User
        {
            Email = UniqueEmail(),
            FullName = "Google User",
            AuthProvider = "google",
            GoogleId = Guid.NewGuid().ToString("N"),
            PasswordHash = null
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }

    [Fact]
    public async Task RequestDataErasure_LocalAccountCorrectPassword_ScrubsIdentityAndDeactivates()
    {
        var email = UniqueEmail();
        var userId = await CreateLocalAccountAsync(email, "P@ssword123");
        var client = _factory.CreateAuthenticatedClient(userId, "Audience");

        var res = await client.PostAsJsonAsync("/api/v1/me/data-erasure", new { CurrentPassword = "P@ssword123" });

        res.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = await db.Users.SingleAsync(u => u.Id == userId);
        user.Email.Should().NotBe(email);
        user.FullName.Should().Be("Người dùng đã xóa");
        user.PasswordHash.Should().BeNull();
        user.IsActive.Should().BeFalse();
        user.DataErasedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task RequestDataErasure_LocalAccountWrongPassword_Returns401AndDoesNotScrub()
    {
        var email = UniqueEmail();
        var userId = await CreateLocalAccountAsync(email, "P@ssword123");
        var client = _factory.CreateAuthenticatedClient(userId, "Audience");

        var res = await client.PostAsJsonAsync("/api/v1/me/data-erasure", new { CurrentPassword = "WrongPassword" });

        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = await db.Users.SingleAsync(u => u.Id == userId);
        user.Email.Should().Be(email);
        user.DataErasedAt.Should().BeNull();
    }

    [Fact]
    public async Task RequestDataErasure_GoogleOnlyAccount_SucceedsWithoutPassword()
    {
        var userId = await CreateGoogleOnlyAccountAsync();
        var client = _factory.CreateAuthenticatedClient(userId, "Audience");

        var res = await client.PostAsJsonAsync("/api/v1/me/data-erasure", new { });

        res.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task RequestDataErasure_TicketHistorySurvives_ButNoLongerIdentifiesTheBuyer()
    {
        var userId = await CreateGoogleOnlyAccountAsync();
        Guid ticketId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var ticket = new Ticket
            {
                Id = Guid.NewGuid(),
                BuyerId = userId,
                PriceId = SeedHelper.TicketPriceId,
                TierId = SeedHelper.TicketTierId,
                ShowId = SeedHelper.ShowId,
                Status = TicketStatus.Confirmed,
                PurchaseChannel = PurchaseChannel.Online,
                CreatedAt = DateTimeOffset.UtcNow
            };
            db.Tickets.Add(ticket);
            await db.SaveChangesAsync();
            ticketId = ticket.Id;
        }

        // §6.4/Luật Kế toán: financial record must survive erasure unchanged in its FK, even though
        // the User row it points to no longer identifies a real person.
        var client = _factory.CreateAuthenticatedClient(userId, "Audience");
        await client.PostAsJsonAsync("/api/v1/me/data-erasure", new { });

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var reloadedTicket = await verifyDb.Tickets.SingleAsync(t => t.Id == ticketId);
        reloadedTicket.BuyerId.Should().Be(userId, "vé vẫn phải giữ liên kết để đối soát tài chính");

        var user = await verifyDb.Users.SingleAsync(u => u.Id == userId);
        user.FullName.Should().Be("Người dùng đã xóa");
    }

    /// <summary>
    /// The handler's own DataErasedAt-not-null guard (409) is defense-in-depth for a path not
    /// reachable through a normal self-service double-call: erasure sets IsActive=false, so
    /// ActiveUserBehavior's pipeline check rejects any second request with 401 before the handler
    /// ever runs again — verified here as the actually-observed behavior. The 409 guard still
    /// matters for e.g. an Admin reactivating an already-erased account.
    /// </summary>
    [Fact]
    public async Task RequestDataErasure_SecondAttemptAfterFirst_Returns401ViaActiveUserPipeline()
    {
        var userId = await CreateGoogleOnlyAccountAsync();
        var client = _factory.CreateAuthenticatedClient(userId, "Audience");
        await client.PostAsJsonAsync("/api/v1/me/data-erasure", new { });

        var secondAttempt = await client.PostAsJsonAsync("/api/v1/me/data-erasure", new { });

        secondAttempt.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RequestDataErasure_AlreadyErasedButReactivatedByAdmin_Returns409()
    {
        var userId = await CreateGoogleOnlyAccountAsync();
        var client = _factory.CreateAuthenticatedClient(userId, "Audience");
        await client.PostAsJsonAsync("/api/v1/me/data-erasure", new { });

        // Simulates an Admin reactivating the account after erasure (ReactivateUserAccountCommand)
        // — IsActive back to true, but DataErasedAt stays set, so the handler's own guard (not the
        // pipeline) must be what catches a second attempt here.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var user = await db.Users.SingleAsync(u => u.Id == userId);
            user.IsActive = true;
            await db.SaveChangesAsync();
        }

        var secondAttempt = await client.PostAsJsonAsync("/api/v1/me/data-erasure", new { });

        secondAttempt.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task AfterDataErasure_ExistingRequestIsRejectedImmediately()
    {
        // ActiveUserBehavior re-checks User.IsActive on every request — erasure flips it false, so
        // this must lock out mid-session, not wait for the JWT to naturally expire.
        var userId = await CreateGoogleOnlyAccountAsync();
        var client = _factory.CreateAuthenticatedClient(userId, "Audience");
        await client.PostAsJsonAsync("/api/v1/me/data-erasure", new { });

        var followUpRequest = await client.GetAsync("/api/v1/me");

        followUpRequest.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
