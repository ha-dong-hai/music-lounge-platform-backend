using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MusicLounge.Domain.Entities;
using MusicLounge.Infrastructure.Persistence;
using MusicLounge.Tests.Integration.Helpers;

namespace MusicLounge.Tests.Integration.Users;

/// <summary>
/// Users self-service — PUT /api/v1/me/profile | POST /api/v1/me/citizen-card | DELETE /api/v1/me
/// </summary>
[Collection("Integration")]
public sealed class ProfileManagementTests
{
    private readonly ApiFactory _factory;

    public ProfileManagementTests(ApiFactory factory) => _factory = factory;

    // ─── UpdateMyProfile ─────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateProfile_ValidData_Returns204AndPersists()
    {
        var client = _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience");

        var res = await client.PutAsJsonAsync("/api/v1/me/profile", new
        {
            FullName = "Nguyen Van A",
            Phone = "0901234567",
            AvatarUrl = "/uploads/avatar-test.png"
        });

        res.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = await db.Users.SingleAsync(u => u.Id == SeedHelper.AudienceId);
        user.FullName.Should().Be("Nguyen Van A");
        user.Phone.Should().Be("0901234567");
        user.AvatarUrl.Should().Be("/uploads/avatar-test.png");
    }

    [Fact]
    public async Task UpdateProfile_EmptyFullName_Returns400()
    {
        var client = _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience");

        var res = await client.PutAsJsonAsync("/api/v1/me/profile", new
        {
            FullName = "",
            Phone = (string?)null,
            AvatarUrl = (string?)null
        });

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ─── SubmitCitizenCard ───────────────────────────────────────────────────

    [Fact]
    public async Task SubmitCitizenCard_ValidData_Returns204AndPersists()
    {
        var client = _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience");
        var cardNumber = UniqueCardNumber();

        var res = await client.PostAsJsonAsync("/api/v1/me/citizen-card", new
        {
            CitizenCardNumber = cardNumber,
            FrontImageUrl = "/uploads/front.png",
            BackImageUrl = "/uploads/back.png"
        });

        res.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = await db.Users.SingleAsync(u => u.Id == SeedHelper.AudienceId);
        user.CitizenCardNumber.Should().Be(cardNumber);
        user.CitizenCardFrontImageUrl.Should().Be("/uploads/front.png");
        user.CitizenCardBackImageUrl.Should().Be("/uploads/back.png");
        user.CitizenCardSubmittedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task SubmitCitizenCard_ResubmitOwnNumber_Returns204()
    {
        var client = _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience");
        var cardNumber = UniqueCardNumber();

        var first = await client.PostAsJsonAsync("/api/v1/me/citizen-card", new
        {
            CitizenCardNumber = cardNumber,
            FrontImageUrl = "/uploads/front.png",
            BackImageUrl = "/uploads/back.png"
        });
        first.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Resubmit same number with new photos — must not conflict with self.
        var second = await client.PostAsJsonAsync("/api/v1/me/citizen-card", new
        {
            CitizenCardNumber = cardNumber,
            FrontImageUrl = "/uploads/front-v2.png",
            BackImageUrl = "/uploads/back-v2.png"
        });
        second.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task SubmitCitizenCard_NumberTakenByAnotherUser_Returns409()
    {
        var ownerClient = _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner");
        var cardNumber = UniqueCardNumber();

        var ownerRes = await ownerClient.PostAsJsonAsync("/api/v1/me/citizen-card", new
        {
            CitizenCardNumber = cardNumber,
            FrontImageUrl = "/uploads/front.png",
            BackImageUrl = "/uploads/back.png"
        });
        ownerRes.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var audienceClient = _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience");
        var audienceRes = await audienceClient.PostAsJsonAsync("/api/v1/me/citizen-card", new
        {
            CitizenCardNumber = cardNumber,
            FrontImageUrl = "/uploads/front2.png",
            BackImageUrl = "/uploads/back2.png"
        });

        audienceRes.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // ─── DeactivateMyAccount ─────────────────────────────────────────────────

    [Fact]
    public async Task DeactivateMyAccount_Returns204AndSetsIsActiveFalse()
    {
        // Dung 1 user rieng (khong phai seed data dung chung) de khong anh huong test khac chay song song.
        var userId = await CreateDedicatedUserAsync();
        var client = _factory.CreateAuthenticatedClient(userId, "Audience");

        var res = await client.DeleteAsync("/api/v1/me");

        res.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = await db.Users.SingleAsync(u => u.Id == userId);
        user.IsActive.Should().BeFalse();
    }

    private static string UniqueCardNumber()
        => Random.Shared.NextInt64(100_000_000, 999_999_999).ToString();

    private async Task<int> CreateDedicatedUserAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = new User
        {
            Email = $"dedicated-{Guid.NewGuid():N}@test.com",
            FullName = "Dedicated Test User",
            IsActive = true,
            EmailVerifiedAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTime.UtcNow
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }
}
