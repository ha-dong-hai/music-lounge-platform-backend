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
        var frontUrl = CreateFakeUploadedImage();
        var backUrl = CreateFakeUploadedImage();

        var res = await client.PostAsJsonAsync("/api/v1/me/citizen-card", new
        {
            CitizenCardNumber = cardNumber,
            FrontImageUrl = frontUrl,
            BackImageUrl = backUrl
        });

        res.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = await db.Users.SingleAsync(u => u.Id == SeedHelper.AudienceId);
        // Encrypted at rest (IPiiEncryptionService) — the stored value is ciphertext, never the
        // raw number; CitizenCardNumberHash (deterministic) is what proves the right value made it in.
        user.CitizenCardNumber.Should().NotBeNullOrEmpty().And.NotBe(cardNumber);
        user.CitizenCardNumberHash.Should().Be(
            Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(cardNumber))));
        var decrypted = scope.ServiceProvider.GetRequiredService<MusicLounge.Application.Common.Interfaces.IPiiEncryptionService>()
            .Decrypt(user.CitizenCardNumber!);
        decrypted.Should().Be(cardNumber);
        // Relocated out of the public wwwroot/uploads tree — the stored value is now an opaque
        // private reference, never the original publicly-guessable URL.
        user.CitizenCardFrontImageUrl.Should().NotBeNullOrEmpty().And.NotBe(frontUrl);
        user.CitizenCardBackImageUrl.Should().NotBeNullOrEmpty().And.NotBe(backUrl);
        user.CitizenCardSubmittedAt.Should().NotBeNull();

        // The public file must have been moved (not copied) out of wwwroot/uploads...
        File.Exists(PublicUploadPath(frontUrl)).Should().BeFalse();
        // ...and be readable back only through the authenticated self-service endpoint.
        var getRes = await client.GetAsync("/api/v1/me/citizen-card/front");
        getRes.StatusCode.Should().Be(HttpStatusCode.OK);
        getRes.Content.Headers.ContentType!.MediaType.Should().Be("image/png");
    }

    [Fact]
    public async Task SubmitCitizenCard_ResubmitOwnNumber_Returns204()
    {
        var client = _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience");
        var cardNumber = UniqueCardNumber();

        var first = await client.PostAsJsonAsync("/api/v1/me/citizen-card", new
        {
            CitizenCardNumber = cardNumber,
            FrontImageUrl = CreateFakeUploadedImage(),
            BackImageUrl = CreateFakeUploadedImage()
        });
        first.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Resubmit same number with new photos — must not conflict with self.
        var second = await client.PostAsJsonAsync("/api/v1/me/citizen-card", new
        {
            CitizenCardNumber = cardNumber,
            FrontImageUrl = CreateFakeUploadedImage(),
            BackImageUrl = CreateFakeUploadedImage()
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
            FrontImageUrl = CreateFakeUploadedImage(),
            BackImageUrl = CreateFakeUploadedImage()
        });
        ownerRes.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Rejected on the uniqueness check before any file is touched, so the audience's URLs
        // never need to resolve to a real file.
        var audienceClient = _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience");
        var audienceRes = await audienceClient.PostAsJsonAsync("/api/v1/me/citizen-card", new
        {
            CitizenCardNumber = cardNumber,
            FrontImageUrl = "/uploads/front2.png",
            BackImageUrl = "/uploads/back2.png"
        });

        audienceRes.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task GetMyCitizenCardImage_NotSubmittedYet_Returns404()
    {
        // Dedicated user (not shared seed data) — other tests in this suite submit a citizen
        // card for the shared Audience/Owner seed users, so only a fresh user is guaranteed clean.
        var userId = await CreateDedicatedUserAsync();
        var client = _factory.CreateAuthenticatedClient(userId, "Audience");

        var res = await client.GetAsync("/api/v1/me/citizen-card/front");

        res.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetCitizenCardImage_AsAdmin_CanViewAnyUsersImage()
    {
        var ownerClient = _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner");
        var submit = await ownerClient.PostAsJsonAsync("/api/v1/me/citizen-card", new
        {
            CitizenCardNumber = UniqueCardNumber(),
            FrontImageUrl = CreateFakeUploadedImage(),
            BackImageUrl = CreateFakeUploadedImage()
        });
        submit.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var adminClient = _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin");
        var res = await adminClient.GetAsync($"/api/v1/admin/users/{SeedHelper.OwnerId}/citizen-card/front");

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        res.Content.Headers.ContentType!.MediaType.Should().Be("image/png");
        res.Headers.CacheControl!.NoStore.Should().BeTrue(
            "CCCD photos must never be cached client-side/by an intermediary proxy");
    }

    [Fact]
    public async Task GetMyCitizenCardImage_AnotherUsersImage_IsNotExposedViaSelfEndpoint()
    {
        var ownerClient = _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner");
        var submit = await ownerClient.PostAsJsonAsync("/api/v1/me/citizen-card", new
        {
            CitizenCardNumber = UniqueCardNumber(),
            FrontImageUrl = CreateFakeUploadedImage(),
            BackImageUrl = CreateFakeUploadedImage()
        });
        submit.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // The self-service endpoint has no target-user parameter to abuse — a different (and
        // itself CCCD-less) user calling it can only ever resolve their own card, never Owner's.
        var otherUserId = await CreateDedicatedUserAsync();
        var otherClient = _factory.CreateAuthenticatedClient(otherUserId, "Audience");
        var res = await otherClient.GetAsync("/api/v1/me/citizen-card/front");

        res.StatusCode.Should().Be(HttpStatusCode.NotFound);
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

    /// <summary>
    /// SubmitCitizenCard relocates its input off disk (LocalFileStorageService.RelocateToPrivateAsync),
    /// so it needs a real file already sitting in wwwroot/uploads — mirroring what a prior
    /// POST /uploads/images call would have produced — rather than a fictional URL string.
    /// </summary>
    private static string CreateFakeUploadedImage()
    {
        var fileName = $"{Guid.NewGuid():N}.png";
        var uploadsDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
        Directory.CreateDirectory(uploadsDir);
        File.WriteAllBytes(Path.Combine(uploadsDir, fileName), [0x89, 0x50, 0x4E, 0x47]);
        return $"/uploads/{fileName}";
    }

    private static string PublicUploadPath(string publicUrl)
        => Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", publicUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));

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
