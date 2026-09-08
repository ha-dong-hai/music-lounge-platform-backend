using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Infrastructure.Persistence;
using MusicLounge.Tests.Integration.Helpers;

namespace MusicLounge.Tests.Integration.Tickets;

/// <summary>
/// MLACP-295. Two endpoints that were missing and three validators that were not there.
///
/// Cancelling a hold matters more than it looks: without it a buyer who changes their mind leaves
/// the seat locked until the expiry sweep runs, while somebody else is trying to buy that seat.
///
/// The job-trigger endpoint needed a list of what may be triggered, because Hangfire no-ops
/// silently on an unknown id — so a typo returns success and the job simply never runs. The version
/// of that list on the other branch had already drifted ten jobs behind, which is why the list is
/// now recorded by the registration itself rather than written out by hand.
/// </summary>
[Collection("Integration")]
public sealed class CancelHoldAndJobTriggerTests
{
    private readonly ApiFactory _factory;

    public CancelHoldAndJobTriggerTests(ApiFactory factory) => _factory = factory;

    private HttpClient Buyer() => _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience");
    private HttpClient Admin() => _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin");

    private async Task<int> HoldOneAsync(HttpClient client)
    {
        var res = await client.PostAsJsonAsync("/api/v1/tickets/holds",
            new { PriceId = SeedHelper.TicketPriceId, Quantity = 1 });
        res.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await res.Content.ReadFromJsonAsync<Envelope<HoldData>>())!.Data.HoldId;
    }

    // ---------- huỷ giữ chỗ ----------

    [Fact]
    public async Task CancellingAHold_ReleasesTheSeatImmediately_InsteadOfWaitingForTheSweep()
    {
        var buyer = Buyer();
        var holdId = await HoldOneAsync(buyer);

        var res = await buyer.DeleteAsync($"/api/v1/tickets/holds/{holdId}");
        res.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.Set<TicketHold>().AnyAsync(h => h.Id == holdId)).Should().BeFalse(
            "leaving the row behind is what kept the seat locked while someone else wanted it");
    }

    [Fact]
    public async Task AHoldBelongingToSomebodyElse_CannotBeCancelled()
    {
        var holdId = await HoldOneAsync(Buyer());

        var res = await _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner")
            .DeleteAsync($"/api/v1/tickets/holds/{holdId}");

        res.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "otherwise anyone could free up seats other buyers are holding");
    }

    [Fact]
    public async Task AHoldAlreadySpentOnAPurchase_CannotBeCancelled()
    {
        // The row backs a real payment by this point. Deleting it on a stale client retry would
        // remove the record standing behind money that has already moved.
        var buyer = Buyer();
        var holdId = await HoldOneAsync(buyer);

        var purchase = await buyer.PostAsJsonAsync("/api/v1/tickets/purchase", new { HoldId = holdId });
        purchase.IsSuccessStatusCode.Should().BeTrue();

        var res = await buyer.DeleteAsync($"/api/v1/tickets/holds/{holdId}");

        res.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await res.Content.ReadAsStringAsync()).Should().Contain("đã được dùng để mua vé");
    }

    [Fact]
    public async Task CancellingAHoldThatDoesNotExist_Is404()
        => (await Buyer().DeleteAsync("/api/v1/tickets/holds/999999")).StatusCode
            .Should().Be(HttpStatusCode.NotFound);

    // ---------- chạy job thủ công ----------

    [Fact]
    public void TheTriggerableJobList_IsWhateverStartupActuallyRegistered()
    {
        using var scope = _factory.Services.CreateScope();
        var ids = scope.ServiceProvider.GetRequiredService<IBackgroundJobService>().GetRecurringJobIds();

        ids.Should().NotBeEmpty().And.OnlyHaveUniqueItems();

        // Spot-checks across the range, including jobs added long after the hand-written list on the
        // other branch was last updated — that list was missing every one of these.
        ids.Should().Contain("release-expired-holds")
            .And.Contain("auto-end-stale-shows")
            .And.Contain("alert-refund-sla-breaches")
            .And.Contain("alert-content-report-sla-breaches");
    }

    [Fact]
    public async Task AdminCanListTheJobs_AndTriggerOneOfThem()
    {
        var list = await Admin().GetAsync("/api/v1/admin/jobs");
        list.StatusCode.Should().Be(HttpStatusCode.OK);

        var ids = (await list.Content.ReadFromJsonAsync<Envelope<List<string>>>())!.Data;
        ids.Should().NotBeEmpty();

        var res = await Admin().PostAsync($"/api/v1/admin/jobs/{ids[0]}/trigger", null);
        res.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task AMistypedJobId_IsRejected_RatherThanSilentlyDoingNothing()
    {
        // This is the whole reason the endpoint validates at all: Hangfire's own behaviour for an
        // unknown id is to no-op, so without this an Admin gets a success response and believes the
        // job ran.
        var res = await Admin().PostAsync("/api/v1/admin/jobs/release-expired-hold/trigger", null);

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await res.Content.ReadAsStringAsync()).Should().Contain("release-expired-holds",
            "the message should list what would have worked");
    }

    [Fact]
    public async Task OnlyAdminsCanTriggerJobs()
    {
        var owner = _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner");

        (await owner.GetAsync("/api/v1/admin/jobs")).StatusCode
            .Should().Be(HttpStatusCode.Forbidden);
        (await owner.PostAsync("/api/v1/admin/jobs/release-expired-holds/trigger", null)).StatusCode
            .Should().Be(HttpStatusCode.Forbidden,
                "several of these jobs move money — releasing settlements, voiding payments");
    }

    private sealed record Envelope<T>(bool Success, T Data);
    private sealed record HoldData(int HoldId, DateTimeOffset ExpiresAt);
}
