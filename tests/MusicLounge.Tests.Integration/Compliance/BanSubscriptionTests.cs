using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.ValueObjects;
using MusicLounge.Infrastructure.Jobs;
using MusicLounge.Infrastructure.Persistence;
using MusicLounge.Tests.Integration.Helpers;
using MusicLoungeVenue = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Tests.Integration.Compliance;

/// <summary>
/// MLACP-369.
/// <list type="bullet">
/// <item>Khoá vĩnh viễn từng ghi một bút toán "hoàn tiền gói theo tỉ lệ" mà không gọi VNPay, không tạo yêu
/// cầu hoàn, không báo chủ — sổ cái nói tiền đã ra, không đồng nào chuyển. Nay: dừng gói, không hoàn phí
/// (thông lệ khi chấm dứt vì vi phạm), nói rõ với chủ.</item>
/// <item>Lệnh khoá đã áp mà bị huỷ thì chỉ nhắn Admin "xử lý thủ công". Nay: tự trả lại đúng phần thời gian
/// gói còn lại lúc bị khoá.</item>
/// <item>Án kháng cáo trong thời gian báo trước rồi bị bác (Upheld) không bao giờ có hiệu lực — job chỉ áp
/// án Active.</item>
/// </list>
/// </summary>
[Collection("Integration")]
public sealed class BanSubscriptionTests
{
    private static readonly TimeSpan PlanLeft = TimeSpan.FromDays(15);

    private readonly ApiFactory _factory;

    public BanSubscriptionTests(ApiFactory factory) => _factory = factory;

    private sealed record Venue(int OwnerId, int LoungeId, int SubscriptionId);

    private async Task<Venue> SeedVenueWithPlanAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var owner = new User { Email = $"ban-owner-{Guid.NewGuid():N}@test.com", FullName = "Ban Owner" };
        db.Users.Add(owner);
        await db.SaveChangesAsync();

        var lounge = new MusicLoungeVenue
        {
            OwnerId = owner.Id, Name = $"BanVenue-{Guid.NewGuid():N}"[..30], Status = LoungeStatus.Approved,
            Address = new VenueAddress { Street = "1 Test St", District = "1", City = "HCM" }
        };
        db.Lounges.Add(lounge);
        var package = new SubscriptionPackage
        {
            Name = $"BanPkg-{Guid.NewGuid():N}"[..20], Price = 300_000m,
            BillingCycle = SubscriptionBillingCycle.Monthly, MaxTicketsPerEvent = 100, IsActive = true
        };
        db.SubscriptionPackages.Add(package);
        await db.SaveChangesAsync();

        var plan = new OwnerSubscription
        {
            OwnerId = owner.Id, PackageId = package.Id,
            StartedAt = DateTimeOffset.UtcNow.AddDays(-15), ExpiresAt = DateTimeOffset.UtcNow + PlanLeft,
            Status = SubscriptionStatus.Active, MaxTicketsPerEventSnapshot = 100
        };
        db.OwnerSubscriptions.Add(plan);
        await db.SaveChangesAsync();
        return new Venue(owner.Id, lounge.Id, plan.Id);
    }

    private async Task<int> SeedDuePenaltyAsync(int loungeId, PenaltyType type, PenaltyStatus status = PenaltyStatus.Active)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var penalty = new VenuePenalty
        {
            LoungeId = loungeId, PenaltyType = type, Reason = "Vi phạm nghiêm trọng",
            IssuedBy = SeedHelper.AdminId, IssuedAt = DateTimeOffset.UtcNow.AddDays(-8),
            EffectiveAt = DateTimeOffset.UtcNow.AddMinutes(-1),
            SuspensionDays = type == PenaltyType.Suspension ? 5 : null, Status = status,
            AppealedAt = status == PenaltyStatus.Upheld ? DateTimeOffset.UtcNow.AddDays(-2) : null,
            AppealResult = status == PenaltyStatus.Upheld ? "Upheld" : null
        };
        db.VenuePenalties.Add(penalty);
        await db.SaveChangesAsync();
        return penalty.Id;
    }

    private async Task ApplyDuePenaltiesAsync()
    {
        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ApplyDuePenaltiesJob>().ExecuteAsync(new JobCancellationToken(false));
    }

    /// <summary>Kháng cáo đang chờ xét cho một án đã áp (cấu hình cho phép kháng cáo sau ngày hiệu lực).</summary>
    private async Task MarkAppealedAsync(int penaltyId, DateTimeOffset appealDeadline)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var penalty = await db.VenuePenalties.SingleAsync(p => p.Id == penaltyId);
        penalty.Status = PenaltyStatus.Appealed;
        penalty.AppealedAt = DateTimeOffset.UtcNow.AddMinutes(-5);
        penalty.AppealDeadline = appealDeadline;
        await db.SaveChangesAsync();
    }

    private Task<HttpResponseMessage> OverturnAsync(int penaltyId)
        => _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin").PostAsJsonAsync(
            $"/api/v1/venue-penalties/{penaltyId}/appeal/review", new { Decision = "Overturned", ReviewNote = "Khoá nhầm" });

    private async Task<List<OwnerSubscription>> PlansOfAsync(int ownerId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.OwnerSubscriptions.AsNoTracking().Where(s => s.OwnerId == ownerId).ToListAsync();
    }

    private async Task<string> LatestNoticeAsync(int userId, NotificationType type)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return (await db.Notifications.Where(n => n.UserId == userId && n.Type == type)
            .OrderByDescending(n => n.Id).FirstAsync()).Body;
    }

    // ─── Án bị bác kháng cáo vẫn phải có hiệu lực ────────────────────────────

    [Fact]
    public async Task ASuspensionUpheldOnAppeal_BeforeItTookEffect_StillTakesEffect()
    {
        var venue = await SeedVenueWithPlanAsync();
        var penaltyId = await SeedDuePenaltyAsync(venue.LoungeId, PenaltyType.Suspension, PenaltyStatus.Upheld);

        await ApplyDuePenaltiesAsync();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.Lounges.SingleAsync(l => l.Id == venue.LoungeId)).Status.Should().Be(LoungeStatus.Suspended,
            "an appeal that was rejected means the penalty stands — it must not vanish");
        (await db.VenuePenalties.SingleAsync(p => p.Id == penaltyId)).AppliedAt.Should().NotBeNull();
    }

    // ─── Khoá vĩnh viễn: dừng gói, không hoàn phí, nói rõ ─────────────────────

    [Fact]
    public async Task ABan_StopsThePlan_BooksNoRefund_AndTellsTheOwnerSo()
    {
        var venue = await SeedVenueWithPlanAsync();
        var penaltyId = await SeedDuePenaltyAsync(venue.LoungeId, PenaltyType.Ban);

        await ApplyDuePenaltiesAsync();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var plan = await db.OwnerSubscriptions.SingleAsync(s => s.Id == venue.SubscriptionId);
        plan.Status.Should().Be(SubscriptionStatus.Cancelled);
        plan.CancelledAt.Should().Be((await db.VenuePenalties.SingleAsync(p => p.Id == penaltyId)).AppliedAt);
        (await db.LedgerEntries.AnyAsync(e => e.ReferenceType == "subscription" && e.ReferenceId == venue.SubscriptionId.ToString()))
            .Should().BeFalse("no money moves, so the ledger must not record a refund");
        (await LatestNoticeAsync(venue.OwnerId, NotificationType.PenaltyIssued))
            .Should().Contain("không được hoàn");
    }

    // ─── Lệnh khoá bị huỷ: trả lại thời gian gói ──────────────────────────────

    [Fact]
    public async Task ABanLiftedOnAppeal_AfterItTookEffect_GivesThePlanItsRemainingTimeBack()
    {
        var venue = await SeedVenueWithPlanAsync();
        var penaltyId = await SeedDuePenaltyAsync(venue.LoungeId, PenaltyType.Ban);
        await ApplyDuePenaltiesAsync();
        await MarkAppealedAsync(penaltyId, DateTimeOffset.UtcNow.AddHours(40));

        (await OverturnAsync(penaltyId)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var plan = (await PlansOfAsync(venue.OwnerId)).Single();
        plan.Status.Should().Be(SubscriptionStatus.Active, "the venue was locked wrongly — its plan comes back");
        plan.ExpiresAt.Should().BeCloseTo(DateTimeOffset.UtcNow + PlanLeft, TimeSpan.FromMinutes(2),
            "exactly the time that was left when the lock took effect");
        (await LatestNoticeAsync(venue.OwnerId, NotificationType.AppealResolved)).Should().Contain("kích hoạt lại");
    }

    [Fact]
    public async Task ABanLifted_AfterTheOwnerBoughtAnotherPlan_AddsTheTimeToThatPlan()
    {
        var venue = await SeedVenueWithPlanAsync();
        var penaltyId = await SeedDuePenaltyAsync(venue.LoungeId, PenaltyType.Ban);
        await ApplyDuePenaltiesAsync();
        int newPlanId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var old = await db.OwnerSubscriptions.SingleAsync(s => s.Id == venue.SubscriptionId);
            var fresh = new OwnerSubscription
            {
                OwnerId = venue.OwnerId, PackageId = old.PackageId, StartedAt = DateTimeOffset.UtcNow,
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(30), Status = SubscriptionStatus.Active,
                MaxTicketsPerEventSnapshot = 100
            };
            db.OwnerSubscriptions.Add(fresh);
            await db.SaveChangesAsync();
            newPlanId = fresh.Id;
        }
        await MarkAppealedAsync(penaltyId, DateTimeOffset.UtcNow.AddHours(40));

        (await OverturnAsync(penaltyId)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var plans = await PlansOfAsync(venue.OwnerId);
        plans.Count(p => p.Status == SubscriptionStatus.Active).Should().Be(1, "an owner holds one active plan");
        plans.Single(p => p.Id == newPlanId).ExpiresAt
            .Should().BeCloseTo(DateTimeOffset.UtcNow.AddDays(30) + PlanLeft, TimeSpan.FromMinutes(2));
    }

    [Fact]
    public async Task ABanLiftedByTheOverdueAppealJob_AlsoGivesThePlanBack()
    {
        var venue = await SeedVenueWithPlanAsync();
        var penaltyId = await SeedDuePenaltyAsync(venue.LoungeId, PenaltyType.Ban);
        await ApplyDuePenaltiesAsync();
        await MarkAppealedAsync(penaltyId, DateTimeOffset.UtcNow.AddMinutes(-1));

        using (var scope = _factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<AutoApproveOverdueAppealsJob>()
                .ExecuteAsync(new JobCancellationToken(false));

        var plan = (await PlansOfAsync(venue.OwnerId)).Single();
        plan.Status.Should().Be(SubscriptionStatus.Active);
        plan.ExpiresAt.Should().BeCloseTo(DateTimeOffset.UtcNow + PlanLeft, TimeSpan.FromMinutes(2));
    }

    [Fact]
    public async Task ALiftedSuspension_KeepsItsCompensation_AndAsksNoAdminToReverseAnythingByHand()
    {
        var venue = await SeedVenueWithPlanAsync();
        var penaltyId = await SeedDuePenaltyAsync(venue.LoungeId, PenaltyType.Suspension);
        await ApplyDuePenaltiesAsync(); // +5 days on the plan as compensation
        await MarkAppealedAsync(penaltyId, DateTimeOffset.UtcNow.AddHours(40));

        (await OverturnAsync(penaltyId)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await PlansOfAsync(venue.OwnerId)).Single().ExpiresAt
            .Should().BeCloseTo(DateTimeOffset.UtcNow + PlanLeft + TimeSpan.FromDays(5), TimeSpan.FromMinutes(2),
                "the days were given for time spent locked — locked wrongly, they are all the more owed");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.Notifications.AnyAsync(n =>
                n.Type == NotificationType.AppealResolved && n.ReferenceId == penaltyId.ToString()
                && n.Title.Contains("xử lý thủ công")))
            .Should().BeFalse("nothing is left for a human to reverse");
    }
}
