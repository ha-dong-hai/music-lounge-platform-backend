using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Infrastructure.Persistence;
using MusicLounge.Tests.Integration.Helpers;
using MusicLoungeVenue = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Tests.Integration.CF1;

/// <summary>
/// W02 Subscription — D12 (package immutability once subscribed), D14 (event-creation gate)
/// POST /api/v1/subscriptions/packages | PUT .../{id} | POST .../subscribe | GET .../vnpay-return | GET .../my
/// </summary>
[Collection("Integration")]
public sealed class SubscriptionTests
{
    private readonly ApiFactory _factory;

    public SubscriptionTests(ApiFactory factory) => _factory = factory;

    // SeedHelper.OwnerId/OtherOwnerId are pre-seeded with an Active subscription (D14 gate for
    // OTHER tests) — subscribe-flow tests need an owner with NO existing subscription instead.
    private static int _freshOwnerCounter = 9100;

    private async Task<int> CreateFreshOwnerAsync()
    {
        var id = Interlocked.Increment(ref _freshOwnerCounter);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Users.Add(new User { Id = id, Email = $"freshowner{id}@test.com", FullName = "Fresh Owner" });
        await db.SaveChangesAsync();
        return id;
    }

    private async Task<int> CreatePackageAsync(decimal price = 500_000m, int maxTicketsPerEvent = 100)
    {
        var client = _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin");
        var res = await client.PostAsJsonAsync("/api/v1/subscriptions/packages", new
        {
            Name = $"Pkg-{Guid.NewGuid():N}",
            Description = "Test package",
            Price = price,
            BillingCycle = "Monthly",
            MaxTicketsPerEvent = maxTicketsPerEvent,
            HasAiPoster = true
        });
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<IdResponse>();
        return body!.Data;
    }

    [Fact]
    public async Task CreatePackage_AsAdmin_Returns201()
    {
        var client = _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin");

        var res = await client.PostAsJsonAsync("/api/v1/subscriptions/packages", new
        {
            Name = "Pro Monthly",
            Description = "test",
            Price = 300_000m,
            BillingCycle = "Monthly",
            MaxTicketsPerEvent = 50,
            HasAiPoster = false
        });

        res.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task CreatePackage_AsNonAdmin_Returns403()
    {
        var client = _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner");

        var res = await client.PostAsJsonAsync("/api/v1/subscriptions/packages", new
        {
            Name = "Pro Monthly", Description = "test", Price = 300_000m,
            BillingCycle = "Monthly", MaxTicketsPerEvent = 50, HasAiPoster = false
        });

        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Subscribe_ThenConfirmViaVnPay_ActivatesSubscription()
    {
        var packageId = await CreatePackageAsync(price: 250_000m);
        var ownerId = await CreateFreshOwnerAsync();
        var ownerClient = _factory.CreateAuthenticatedClient(ownerId, "Owner");

        var subscribeRes = await ownerClient.PostAsJsonAsync(
            "/api/v1/subscriptions/subscribe", new { PackageId = packageId });
        subscribeRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var initiation = await subscribeRes.Content.ReadFromJsonAsync<SubscriptionInitiationResponse>();

        var noRedirectClient = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var callbackRes = await noRedirectClient.GetAsync(
            $"/api/v1/subscriptions/vnpay-return?vnp_TxnRef={initiation!.Data.OrderId}" +
            $"&vnp_ResponseCode=00&vnp_Amount={(long)(initiation.Data.Amount * 100)}");
        callbackRes.StatusCode.Should().Be(HttpStatusCode.Redirect);

        var myRes = await ownerClient.GetAsync("/api/v1/subscriptions/my");
        var body = await myRes.Content.ReadAsStringAsync();
        body.Should().Contain("\"status\":\"Active\"");
    }

    [Fact]
    public async Task CreateLoungeShow_WithoutActiveSubscription_Returns422()
    {
        int newOwnerId, newLoungeId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var user = new User { Id = 9001, Email = "nosub-owner@test.com", FullName = "NoSub Owner" };
            db.Users.Add(user);
            var lounge = new MusicLoungeVenue
            {
                Id = 9001, OwnerId = 9001, Name = "NoSub Lounge",
                Address = new MusicLounge.Domain.ValueObjects.VenueAddress
                {
                    Street = "1 Test", District = "1", City = "HCM"
                }
            };
            db.Lounges.Add(lounge);
            await db.SaveChangesAsync();
            newOwnerId = user.Id;
            newLoungeId = lounge.Id;
        }

        var client = _factory.CreateAuthenticatedClient(newOwnerId, "Owner", newLoungeId);
        var res = await client.PostAsJsonAsync("/api/v1/lounge-shows", new
        {
            LoungeId = newLoungeId,
            Name = "No Sub Show",
            Description = "test",
            Format = "Offline",
            ScheduledStart = DateTimeOffset.UtcNow.AddDays(20),
            ScheduledEnd = (DateTimeOffset?)null,
            CategoryId = (int?)null,
            OfflineQuota = 50,
            OnlineQuota = (int?)null,
            GenreIds = Array.Empty<int>(),
            Performances = Array.Empty<object>()
        });

        res.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task CreateLoungeShow_WithActiveSubscription_Returns201()
    {
        // SeedHelper.OwnerId is pre-seeded with an Active subscription (see SeedHelper.cs)
        var client = _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner", SeedHelper.LoungeId);

        var res = await client.PostAsJsonAsync("/api/v1/lounge-shows", new
        {
            LoungeId = SeedHelper.LoungeId,
            Name = "Has Sub Show",
            Description = "test",
            Format = "Offline",
            ScheduledStart = DateTimeOffset.UtcNow.AddDays(20),
            ScheduledEnd = (DateTimeOffset?)null,
            CategoryId = (int?)null,
            OfflineQuota = 50,
            OnlineQuota = (int?)null,
            GenreIds = Array.Empty<int>(),
            Performances = Array.Empty<object>()
        });

        res.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task UpdatePackage_PriceChange_BlockedWhenActiveSubscriberExists()
    {
        var packageId = await CreatePackageAsync();
        var ownerId = await CreateFreshOwnerAsync();
        var ownerClient = _factory.CreateAuthenticatedClient(ownerId, "Owner");
        var subscribeRes = await ownerClient.PostAsJsonAsync(
            "/api/v1/subscriptions/subscribe", new { PackageId = packageId });
        var initiation = await subscribeRes.Content.ReadFromJsonAsync<SubscriptionInitiationResponse>();
        await ownerClient.GetAsync(
            $"/api/v1/subscriptions/vnpay-return?vnp_TxnRef={initiation!.Data.OrderId}" +
            $"&vnp_ResponseCode=00&vnp_Amount={(long)(initiation.Data.Amount * 100)}");

        var adminClient = _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin");
        var res = await adminClient.PutAsJsonAsync($"/api/v1/subscriptions/packages/{packageId}", new
        {
            Description = "Updated", Price = 999_999m, MaxTicketsPerEvent = 100, HasAiPoster = true, IsActive = true
        });

        res.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task UpdatePackage_DescriptionOnly_AlwaysAllowed()
    {
        var packageId = await CreatePackageAsync();
        var adminClient = _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin");

        var res = await adminClient.PutAsJsonAsync($"/api/v1/subscriptions/packages/{packageId}", new
        {
            Description = "New description", Price = 500_000m, MaxTicketsPerEvent = 100,
            HasAiPoster = true, IsActive = true
        });

        res.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    private sealed record IdResponse(bool Success, int Data);
    private sealed record SubscriptionInitiationData(int PaymentId, string OrderId, decimal Amount, string PaymentUrl);
    private sealed record SubscriptionInitiationResponse(bool Success, SubscriptionInitiationData Data);
}
