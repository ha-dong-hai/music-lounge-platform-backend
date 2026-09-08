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
/// MLACP-287. A guest could file a complaint (POST /complaints is AllowAnonymous) and then had no
/// way whatsoever to learn the outcome: GET /complaints/my requires authentication, there was no
/// lookup endpoint, and ISmsService has only one method — sending a phone verification code — so
/// there was no resolution SMS either. The complaint went into a void.
///
/// The reference is a random string rather than the row id: the lookup endpoint is public, so a
/// guessable reference would mean reading other people's complaints by counting upwards.
/// </summary>
[Collection("Integration")]
public sealed class GuestComplaintLookupTests
{
    private readonly ApiFactory _factory;

    public GuestComplaintLookupTests(ApiFactory factory) => _factory = factory;

    private async Task<int> SeedShowAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var show = new LoungeShow
        {
            LoungeId = SeedHelper.LoungeId,
            Name = $"GuestComplaintShow-{Guid.NewGuid():N}",
            Description = "Integration test show",
            Format = LoungeShowFormat.Offline,
            Status = LoungeShowStatus.Published,
            ScheduledStart = DateTimeOffset.UtcNow.AddDays(4),
            ScheduledEnd = DateTimeOffset.UtcNow.AddDays(4).AddHours(3)
        };
        db.LoungeShows.Add(show);
        await db.SaveChangesAsync();
        return show.Id;
    }

    private async Task<Envelope<Created>> FileAsGuestAsync(int showId)
    {
        var guest = _factory.CreateClient();   // no token — this is the whole point
        var res = await guest.PostAsJsonAsync("/api/v1/complaints", new
        {
            TargetType = "show",
            TargetId = showId,
            Category = "EventMisrepresentation",
            Description = "Nội dung quảng cáo không đúng với thực tế",
            EvidenceUrls = (string?)null,
            ContactPhone = "0900000001"
        });
        res.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await res.Content.ReadFromJsonAsync<Envelope<Created>>())!;
    }

    [Fact]
    public async Task GuestGetsALookupReference_AndCanReadTheOutcomeWithIt()
    {
        var showId = await SeedShowAsync();
        var created = await FileAsGuestAsync(showId);

        created.Data.LookupReference.Should().NotBeNullOrWhiteSpace(
            "without this the guest has no way at all to find out what happened");
        created.Data.LookupReference!.Should().NotBe(created.Data.Id.ToString(),
            "a reference equal to the row id would let anyone read other complaints by counting");

        // Admin resolves it, still with no account involved on the complainant's side.
        var admin = _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin");
        var resolve = await admin.PostAsJsonAsync($"/api/v1/complaints/{created.Data.Id}/resolve",
            new { Status = "Rejected", ResolvedAction = "Dismiss", Resolution = "Không đủ căn cứ" });
        resolve.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var guest = _factory.CreateClient();
        var lookup = await guest.GetAsync($"/api/v1/complaints/lookup/{created.Data.LookupReference}");
        lookup.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await lookup.Content.ReadFromJsonAsync<Envelope<LookupResult>>();
        body!.Data.Id.Should().Be(created.Data.Id);
        body.Data.Status.Should().Be("Rejected");
        body.Data.Resolution.Should().Be("Không đủ căn cứ",
            "the outcome is the entire reason the guest comes back");
    }

    [Fact]
    public async Task UnknownReference_IsIndistinguishableFromAWrongOne()
    {
        var guest = _factory.CreateClient();

        var res = await guest.GetAsync($"/api/v1/complaints/lookup/{Guid.NewGuid():N}");

        res.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "this endpoint is public — telling a caller that a reference exists but is not theirs " +
            "would turn it into a probe");
    }

    [Fact]
    public async Task AuthenticatedComplainant_GetsNoReference_BecauseTheyHaveTheirOwnList()
    {
        var showId = await SeedShowAsync();
        var client = _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience");

        var res = await client.PostAsJsonAsync("/api/v1/complaints", new
        {
            TargetType = "show",
            TargetId = showId,
            Category = "EventMisrepresentation",
            Description = "Khiếu nại từ tài khoản đã đăng nhập",
            EvidenceUrls = (string?)null,
            ContactPhone = (string?)null
        });
        res.StatusCode.Should().Be(HttpStatusCode.Created);

        var created = await res.Content.ReadFromJsonAsync<Envelope<Created>>();
        created!.Data.LookupReference.Should().BeNull(
            "a signed-in user reads their complaints through /complaints/my; handing out a second, " +
            "shareable credential for the same data would only widen the exposure");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.Set<Complaint>().SingleAsync(c => c.Id == created.Data.Id))
            .LookupReference.Should().BeNull();
    }

    private sealed record Envelope<T>(bool Success, T Data);
    private sealed record Created(int Id, string? LookupReference);
    private sealed record LookupResult(
        int Id, string TargetType, string Category, string Status,
        string? Resolution, string? ResolvedAction,
        DateTimeOffset CreatedAt, DateTimeOffset? ResolvedAt, DateTimeOffset? SlaDeadline);
}
