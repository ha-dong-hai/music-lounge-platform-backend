using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Infrastructure.Persistence;
using MusicLounge.Tests.Integration.Helpers;

namespace MusicLounge.Tests.Integration.CF7;

/// <summary>
/// CF7 — Owner / Admin analytics dashboards
/// GET /api/v1/analytics/my-lounge | GET /api/v1/analytics/platform
/// </summary>
[Collection("Integration")]
public sealed class AnalyticsTests
{
    private readonly ApiFactory _factory;

    public AnalyticsTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task GetMyLounge_AsOwner_Returns200()
    {
        var client = _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner");

        var res = await client.GetAsync($"/api/v1/analytics/my-lounge?loungeId={SeedHelper.LoungeId}");

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await res.Content.ReadAsStringAsync();
        body.Should().Contain("\"totalShows\"");
    }

    [Fact]
    public async Task GetMyLounge_AsUnrelatedOwner_Returns403()
    {
        var client = _factory.CreateAuthenticatedClient(SeedHelper.OtherOwnerId, "Owner");

        var res = await client.GetAsync($"/api/v1/analytics/my-lounge?loungeId={SeedHelper.LoungeId}");

        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetMyLounge_AsAudience_Returns403()
    {
        var client = _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience");

        var res = await client.GetAsync($"/api/v1/analytics/my-lounge?loungeId={SeedHelper.LoungeId}");

        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetPlatform_AsAdmin_Returns200()
    {
        var client = _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin");

        var res = await client.GetAsync("/api/v1/analytics/platform");

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await res.Content.ReadAsStringAsync();
        body.Should().Contain("\"totalVenues\"");
    }

    [Fact]
    public async Task GetPlatform_AsOwner_Returns403()
    {
        var client = _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner");

        var res = await client.GetAsync("/api/v1/analytics/platform");

        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetPlatform_SumsGmvAndDonationsAcrossMultipleRows()
    {
        // GetPlatformAnalyticsQueryHandler used to FindAsync() every matching Payment/Donation row
        // (full entities) just to .Sum() one column client-side — now goes through
        // IRepository<T,TKey>.SumAsync (narrow projection). Verifying against a delta (not an
        // absolute total) because seed/other-test data already contributes to these sums.
        var client = _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin");
        var before = await GetPlatformAsync(client);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Payments.AddRange(
                MakeConfirmedPayment(111_000m),
                MakeConfirmedPayment(222_000m));
            db.Donations.AddRange(
                MakeConfirmedDonation(50_000m),
                MakeConfirmedDonation(75_000m));
            await db.SaveChangesAsync();
        }

        var after = await GetPlatformAsync(client);

        (after.TotalGrossMerchandiseValue - before.TotalGrossMerchandiseValue).Should().Be(333_000m);
        (after.TotalDonationVolume - before.TotalDonationVolume).Should().Be(125_000m);
    }

    private static Payment MakeConfirmedPayment(decimal grossAmount) => new()
    {
        OrderId = $"test-{Guid.NewGuid():N}",
        GrossAmount = grossAmount,
        Status = PaymentStatus.Confirmed,
        ReferenceType = "TicketHold",
        ReferenceId = "0",
        CreatedAt = DateTimeOffset.UtcNow
    };

    private static Donation MakeConfirmedDonation(decimal gross) => new()
    {
        PerformanceId = SeedHelper.PerformanceId,
        Gross = gross,
        Net = gross,
        Status = DonationStatus.PendingOwnerAck,
        CreatedAt = DateTimeOffset.UtcNow
    };

    private async Task<PlatformAnalyticsData> GetPlatformAsync(HttpClient client)
    {
        var res = await client.GetAsync("/api/v1/analytics/platform");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await res.Content.ReadFromJsonAsync<PlatformAnalyticsResponse>();
        return body!.Data;
    }

    private sealed record PlatformAnalyticsResponse(bool Success, PlatformAnalyticsData Data);

    private sealed record PlatformAnalyticsData(
        int TotalVenues, int TotalPublishedShows, int TotalUsers, int TotalTicketsSold,
        decimal TotalGrossMerchandiseValue, decimal TotalDonationVolume, int PendingModerationsCount);
}
