using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Hangfire;
using Microsoft.Extensions.DependencyInjection;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Settlements;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.ValueObjects;
using MusicLounge.Infrastructure.Jobs;
using MusicLounge.Infrastructure.Persistence;
using MusicLounge.Tests.Integration.Helpers;
using MusicLoungeVenue = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Tests.Integration.CF4;

/// <summary>
/// MLACP-664. Sao kê công khai ghi "nền tảng đang giữ — chờ chuyển cho phòng trà" mà không nói KHI NÀO chuyển (chủ dự án
/// 05/10/2026). Nay khoản nền tảng còn giữ có thời điểm dự kiến (lần chạy kế tiếp của job giải ngân), hoặc báo đang bị giữ khi
/// phòng trà chưa đủ điều kiện nhận tiền. Đi qua đường thật: tạo donate → IPN → (job giải ngân) → đọc trang công khai.
/// </summary>
[Collection("Integration")]
public sealed class PlatformPayoutExpectedTests
{
    private const decimal Amount = 100_000m;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly ApiFactory _factory;

    public PlatformPayoutExpectedTests(ApiFactory factory) => _factory = factory;

    private sealed record Wrapped<T>(T Data);
    private sealed record InitData(Guid DonationId, string OrderId);
    private sealed record Page(List<Entry> Items);
    private sealed record Entry(Guid Id, string Stage, DateTimeOffset? PlatformPaidVenueAt, DateTimeOffset? PlatformPayoutExpectedAt, bool PlatformPayoutHeld);

    private async Task<(Guid PerformerId, Guid PerformanceId)> SeedAsync(bool ownerApproved)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var pii = scope.ServiceProvider.GetRequiredService<IPiiEncryptionService>();
        var owner = new User
        {
            Email = $"payout664-{Guid.NewGuid():N}@test.com", FullName = "Chủ phòng trà 664",
            CitizenCardSubmittedAt = DateTimeOffset.UtcNow.AddDays(-30),
            CitizenCardReviewStatus = ownerApproved ? KycReviewStatus.Approved : KycReviewStatus.Pending
        };
        db.Users.Add(owner);
        await db.SaveChangesAsync();
        var lounge = new MusicLoungeVenue
        {
            OwnerId = owner.Id, Name = $"Payout664-{Guid.NewGuid():N}"[..30], Status = LoungeStatus.Approved,
            Address = new VenueAddress { Street = "1 Test St", District = "1", City = "HCM" }
        };
        db.Lounges.Add(lounge);
        await db.SaveChangesAsync();
        var start = DateTimeOffset.UtcNow.AddHours(-1);
        var show = new LoungeShow
        {
            LoungeId = lounge.Id, Name = $"Payout664Show-{Guid.NewGuid():N}", Description = "test", Format = LoungeShowFormat.Online,
            Status = LoungeShowStatus.Ongoing, ScheduledStart = start, ScheduledEnd = start.AddHours(3), VcpmcRoyaltyReference = "VCPMC-TEST"
        };
        var performer = new Performer { Name = $"Artist664-{Guid.NewGuid():N}"[..20], CreatedByUserId = owner.Id };
        db.Add(show);
        db.Add(performer);
        await db.SaveChangesAsync();
        db.Add(new BankAccount
        {
            OwnerType = BankAccountOwnerType.Lounge, OwnerId = lounge.Id, BankName = "Test Bank",
            AccountNumber = pii.Encrypt("0000006641"), AccountHolder = "Chu phong tra 664", IsDefault = true, IsVerified = true
        });
        var performance = new Performance { LoungeShowId = show.Id, PerformerId = performer.Id };
        db.Add(new Livestream { LoungeShowId = show.Id, Status = LivestreamStatus.Live, StartedAt = start, IsFree = true });
        db.Add(performance);
        await db.SaveChangesAsync();
        return (performer.Id, performance.Id);
    }

    private async Task<Guid> DonateAsync(Guid performanceId)
    {
        var res = await _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience").PostAsJsonAsync("/api/v1/donations",
            new { PerformanceId = performanceId, Amount, IsAnonymous = false, Message = "Hay quá", IsMessagePublic = true });
        res.StatusCode.Should().Be(HttpStatusCode.Created);
        var init = (await res.Content.ReadFromJsonAsync<Wrapped<InitData>>())!.Data;
        (await _factory.CreateClient().GetAsync(
                $"/api/v1/donations/vnpay-ipn?vnp_TxnRef={Uri.EscapeDataString(init.OrderId)}&vnp_ResponseCode=00&vnp_Amount={(long)(Amount * 100)}"))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        return init.DonationId;
    }

    private async Task<Entry> EntryAsync(Guid performerId, Guid donationId)
    {
        var raw = await (await _factory.CreateClient().GetAsync($"/api/v1/performers/{performerId}/donations?pageSize=100"))
            .Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<Wrapped<Page>>(raw, Json)!.Data.Items.Single(e => e.Id == donationId);
    }

    [Fact]
    public async Task WhileThePlatformHoldsIt_TheStatementSaysWhenTheVenueWillBePaid()
    {
        var (performerId, performanceId) = await SeedAsync(ownerApproved: true);
        var donationId = await DonateAsync(performanceId);
        var now = DateTimeOffset.UtcNow;

        var held = await EntryAsync(performerId, donationId);
        held.Stage.Should().Be("PlatformHolding", "test premise");
        held.PlatformPayoutHeld.Should().BeFalse();
        held.PlatformPayoutExpectedAt.Should().NotBeNull("the reader must see when the platform pays the venue");
        held.PlatformPayoutExpectedAt!.Value.TimeOfDay.Should().Be(TimeSpan.Zero, "the release job runs at 00:00 UTC");
        held.PlatformPayoutExpectedAt.Value.Should().BeAfter(now).And.BeOnOrBefore(now.AddDays(1));

        using (var scope = _factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<SettlementReleaseJob>().ExecuteAsync(new JobCancellationToken(false));

        var paid = await EntryAsync(performerId, donationId);
        paid.PlatformPaidVenueAt.Should().NotBeNull("test premise: the job paid the venue");
        paid.PlatformPayoutExpectedAt.Should().BeNull("once paid there is nothing left to expect");
    }

    [Fact]
    public async Task WhenTheVenueCannotBePaidYet_TheStatementSaysTheMoneyIsHeld_NotADate()
    {
        var (performerId, performanceId) = await SeedAsync(ownerApproved: false);
        var donationId = await DonateAsync(performanceId);

        var held = await EntryAsync(performerId, donationId);
        held.PlatformPayoutHeld.Should().BeTrue("the release job holds payouts to an owner whose ID is not approved (MLACP-395)");
        held.PlatformPayoutExpectedAt.Should().BeNull("a date the job will not keep is a promise without a mechanism");
    }

    [Fact]
    public void TheScheduleIsDailyAtUtcMidnight_WhichIsWhatNextRunAtAssumes()
    {
        SettlementReleaseSchedule.Cron.Should().Be(Cron.Daily(), "NextRunAt computes the next 00:00 UTC — change both together");
        var t = new DateTimeOffset(2026, 10, 5, 10, 32, 0, TimeSpan.Zero);
        SettlementReleaseSchedule.NextRunAt(t, t.AddMinutes(1)).Should().Be(new DateTimeOffset(2026, 10, 6, 0, 0, 0, TimeSpan.Zero));
        SettlementReleaseSchedule.NextRunAt(t.AddDays(3), t).Should().Be(new DateTimeOffset(2026, 10, 9, 0, 0, 0, TimeSpan.Zero),
            "a tranche scheduled in the future is picked up by the first run on or after its date");
        var midnight = new DateTimeOffset(2026, 10, 6, 0, 0, 0, TimeSpan.Zero);
        SettlementReleaseSchedule.NextRunAt(midnight, midnight).Should().Be(midnight);
    }
}
