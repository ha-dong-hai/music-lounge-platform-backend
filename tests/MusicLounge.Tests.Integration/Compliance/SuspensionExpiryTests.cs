using FluentAssertions;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Infrastructure.Jobs;
using MusicLounge.Infrastructure.Persistence;
using MusicLounge.Tests.Integration.Helpers;

namespace MusicLounge.Tests.Integration.Compliance;

/// <summary>
/// MLACP-299. ApplyDuePenaltiesJob suspended a venue and stopped there. The only route back to
/// Approved was an appeal, so a venue that did not appeal stayed suspended for good — while the
/// notification it received said "sẽ bị tạm khoá N ngày". A suspended venue cannot publish shows,
/// so this was a permanent shutdown described as a temporary one.
///
/// The model had anticipated the ending — VenuePenalty.SuspensionEnd and PenaltyStatus.Expired both
/// existed — and nothing wrote either.
/// </summary>
[Collection("Integration")]
public sealed class SuspensionExpiryTests
{
    private readonly ApiFactory _factory;

    public SuspensionExpiryTests(ApiFactory factory) => _factory = factory;

    private async Task<(int LoungeId, int OwnerId)> SeedSuspendedVenueAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var owner = new User
        {
            Email = $"penalty-{Guid.NewGuid():N}@test.com",
            FullName = "Chủ Phòng Trà Bị Khoá",
            Role = UserRole.Owner,
            AuthProvider = "local",
            EmailVerifiedAt = DateTimeOffset.UtcNow,
            IsActive = true
        };
        db.Users.Add(owner);
        await db.SaveChangesAsync();

        var lounge = new MusicLounge.Domain.Entities.MusicLounge
        {
            OwnerId = owner.Id,
            Name = $"SuspendedVenue-{Guid.NewGuid():N}",
            Description = "Integration test venue",
            Status = LoungeStatus.Suspended
        };
        db.Add(lounge);
        await db.SaveChangesAsync();
        return (lounge.Id, owner.Id);
    }

    private async Task<int> SeedAppliedSuspensionAsync(
        int loungeId, int suspensionDays, DateTimeOffset appliedAt, DateTimeOffset? suspensionEnd)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var penalty = new VenuePenalty
        {
            LoungeId = loungeId,
            PenaltyType = PenaltyType.Suspension,
            Reason = "Vi phạm nội dung",
            IssuedBy = SeedHelper.AdminId,
            IssuedAt = appliedAt,
            EffectiveAt = appliedAt,
            SuspensionDays = suspensionDays,
            SuspensionEnd = suspensionEnd,
            AppliedAt = appliedAt,
            Status = PenaltyStatus.Active
        };
        db.Add(penalty);
        await db.SaveChangesAsync();
        return penalty.Id;
    }

    private async Task RunJobAsync()
    {
        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ExpireServedSuspensionsJob>()
            .ExecuteAsync(new JobCancellationToken(false));
    }

    private async Task<(LoungeStatus Status, PenaltyStatus Penalty, DateTimeOffset? End)> ReadAsync(
        int loungeId, int penaltyId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var lounge = await db.Set<MusicLounge.Domain.Entities.MusicLounge>()
            .SingleAsync(l => l.Id == loungeId);
        var penalty = await db.Set<VenuePenalty>().SingleAsync(p => p.Id == penaltyId);
        return (lounge.Status, penalty.Status, penalty.SuspensionEnd);
    }

    [Fact]
    public async Task AServedSuspension_LiftsItself_WithoutAnyoneAppealing()
    {
        var (loungeId, ownerId) = await SeedSuspendedVenueAsync();
        var penaltyId = await SeedAppliedSuspensionAsync(
            loungeId, suspensionDays: 7,
            appliedAt: DateTimeOffset.UtcNow.AddDays(-8),
            suspensionEnd: DateTimeOffset.UtcNow.AddDays(-1));

        await RunJobAsync();

        var (status, penalty, _) = await ReadAsync(loungeId, penaltyId);
        status.Should().Be(LoungeStatus.Approved,
            "the venue was told the lock lasts 7 days — nothing should be required of them for it to end");
        penalty.Should().Be(PenaltyStatus.Expired);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.Notifications.AnyAsync(
                n => n.UserId == ownerId && n.Type == NotificationType.PenaltyExpired))
            .Should().BeTrue("the owner has no other way to learn they can operate again");
    }

    [Fact]
    public async Task ASuspensionStillRunning_IsLeftAlone()
    {
        var (loungeId, _) = await SeedSuspendedVenueAsync();
        var penaltyId = await SeedAppliedSuspensionAsync(
            loungeId, suspensionDays: 7,
            appliedAt: DateTimeOffset.UtcNow.AddDays(-2),
            suspensionEnd: DateTimeOffset.UtcNow.AddDays(5));

        await RunJobAsync();

        var (status, penalty, _) = await ReadAsync(loungeId, penaltyId);
        status.Should().Be(LoungeStatus.Suspended);
        penalty.Should().Be(PenaltyStatus.Active);
    }

    [Fact]
    public async Task ASuspensionAppliedBeforeThisColumnExisted_IsStillRescued()
    {
        // The case that decides whether this fix helps anybody: every venue suspended before
        // MLACP-299 has SuspensionEnd = null. Skipping those would mean the venues locked out
        // today are exactly the ones that stay locked out.
        var (loungeId, _) = await SeedSuspendedVenueAsync();
        var penaltyId = await SeedAppliedSuspensionAsync(
            loungeId, suspensionDays: 3,
            appliedAt: DateTimeOffset.UtcNow.AddDays(-10),
            suspensionEnd: null);

        await RunJobAsync();

        var (status, penalty, end) = await ReadAsync(loungeId, penaltyId);
        status.Should().Be(LoungeStatus.Approved);
        penalty.Should().Be(PenaltyStatus.Expired);
        end.Should().NotBeNull("the deadline is written back so the owner can see it on their screen");
    }

    [Fact]
    public async Task ASecondSuspensionStillRunning_KeepsTheVenueLocked()
    {
        // Two overlapping suspensions: lifting on the earlier one would release the venue sooner
        // than it was sentenced to. Same rule ReviewAppeal already applies.
        var (loungeId, _) = await SeedSuspendedVenueAsync();
        var servedId = await SeedAppliedSuspensionAsync(
            loungeId, 7, DateTimeOffset.UtcNow.AddDays(-8), DateTimeOffset.UtcNow.AddDays(-1));
        await SeedAppliedSuspensionAsync(
            loungeId, 30, DateTimeOffset.UtcNow.AddDays(-2), DateTimeOffset.UtcNow.AddDays(28));

        await RunJobAsync();

        var (status, penalty, _) = await ReadAsync(loungeId, servedId);
        penalty.Should().Be(PenaltyStatus.Expired, "this particular sentence has been served");
        status.Should().Be(LoungeStatus.Suspended,
            "but the venue is still serving another one");
    }

    [Fact]
    public async Task AnOpenEndedSuspension_NeverExpiresOnItsOwn()
    {
        // SuspensionDays null means no fixed term — there is nothing to count down.
        var (loungeId, _) = await SeedSuspendedVenueAsync();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Add(new VenuePenalty
            {
                LoungeId = loungeId,
                PenaltyType = PenaltyType.Suspension,
                Reason = "Tạm khoá chờ điều tra",
                IssuedBy = SeedHelper.AdminId,
                IssuedAt = DateTimeOffset.UtcNow.AddDays(-30),
                EffectiveAt = DateTimeOffset.UtcNow.AddDays(-30),
                SuspensionDays = null,
                AppliedAt = DateTimeOffset.UtcNow.AddDays(-30),
                Status = PenaltyStatus.Active
            });
            await db.SaveChangesAsync();
        }

        await RunJobAsync();

        using var verify = _factory.Services.CreateScope();
        var vdb = verify.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await vdb.Set<MusicLounge.Domain.Entities.MusicLounge>().SingleAsync(l => l.Id == loungeId))
            .Status.Should().Be(LoungeStatus.Suspended);
    }

    [Fact]
    public async Task AVenueAnAdminHasAlreadyMovedOn_IsNotOverwritten()
    {
        // The sentence is over, but a human has since put the venue somewhere else deliberately.
        // The penalty still closes; the venue's status is not the job's to reassign.
        var (loungeId, _) = await SeedSuspendedVenueAsync();
        var penaltyId = await SeedAppliedSuspensionAsync(
            loungeId, 7, DateTimeOffset.UtcNow.AddDays(-8), DateTimeOffset.UtcNow.AddDays(-1));

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var lounge = await db.Set<MusicLounge.Domain.Entities.MusicLounge>()
                .SingleAsync(l => l.Id == loungeId);
            lounge.Status = LoungeStatus.Locked;
            await db.SaveChangesAsync();
        }

        await RunJobAsync();

        var (status, penalty, _) = await ReadAsync(loungeId, penaltyId);
        penalty.Should().Be(PenaltyStatus.Expired);
        status.Should().Be(LoungeStatus.Locked, "an Admin's own decision outranks the countdown");
    }

    [Fact]
    public async Task ASuspensionWhoseAppealWasRejected_StillExpiresWhenItsTermIsServed()
    {
        // MLACP-304. Losing an appeal moves the penalty from Appealed to Upheld — it still stands,
        // it has just been through review. The expiry job originally filtered on Active only, so
        // this venue was skipped entirely: appeal, lose, stay locked forever. Exactly the hole
        // MLACP-299 was written to close, reached by a different route.
        var (loungeId, _) = await SeedSuspendedVenueAsync();
        var penaltyId = await SeedAppliedSuspensionAsync(
            loungeId, 7, DateTimeOffset.UtcNow.AddDays(-8), DateTimeOffset.UtcNow.AddDays(-1));

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var penalty = await db.Set<VenuePenalty>().SingleAsync(p => p.Id == penaltyId);
            penalty.Status = PenaltyStatus.Upheld;
            penalty.AppealResult = "Upheld";
            await db.SaveChangesAsync();
        }

        await RunJobAsync();

        var (status, penaltyStatus, _) = await ReadAsync(loungeId, penaltyId);
        penaltyStatus.Should().Be(PenaltyStatus.Expired);
        status.Should().Be(LoungeStatus.Approved,
            "losing an appeal means serving the sentence, not serving it forever");
    }

    [Fact]
    public async Task AnUpheldSuspensionElsewhere_StillKeepsTheVenueLocked()
    {
        // The same omission in the other direction: the "any other penalty still in force" count
        // ignored Upheld too, so a venue serving an upheld sentence could be released early when a
        // different penalty happened to expire.
        var (loungeId, _) = await SeedSuspendedVenueAsync();
        var servedId = await SeedAppliedSuspensionAsync(
            loungeId, 7, DateTimeOffset.UtcNow.AddDays(-8), DateTimeOffset.UtcNow.AddDays(-1));
        var upheldId = await SeedAppliedSuspensionAsync(
            loungeId, 30, DateTimeOffset.UtcNow.AddDays(-2), DateTimeOffset.UtcNow.AddDays(28));

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var other = await db.Set<VenuePenalty>().SingleAsync(p => p.Id == upheldId);
            other.Status = PenaltyStatus.Upheld;
            await db.SaveChangesAsync();
        }

        await RunJobAsync();

        var (status, penaltyStatus, _) = await ReadAsync(loungeId, servedId);
        penaltyStatus.Should().Be(PenaltyStatus.Expired);
        status.Should().Be(LoungeStatus.Suspended,
            "the other sentence was upheld, which means it still stands");
    }

    [Fact]
    public async Task APendingAppeal_IsLeftToTheAppealProcess()
    {
        // Appealed is deliberately outside the set: an appeal under review is decided by the review,
        // and AutoApproveOverdueAppealsJob already bounds how long that can drag on.
        var (loungeId, _) = await SeedSuspendedVenueAsync();
        var penaltyId = await SeedAppliedSuspensionAsync(
            loungeId, 7, DateTimeOffset.UtcNow.AddDays(-8), DateTimeOffset.UtcNow.AddDays(-1));

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var penalty = await db.Set<VenuePenalty>().SingleAsync(p => p.Id == penaltyId);
            penalty.Status = PenaltyStatus.Appealed;
            penalty.AppealedAt = DateTimeOffset.UtcNow.AddDays(-1);
            await db.SaveChangesAsync();
        }

        await RunJobAsync();

        (await ReadAsync(loungeId, penaltyId)).Penalty.Should().Be(PenaltyStatus.Appealed);
    }

    [Fact]
    public void TheJobIsRegistered_SoItActuallyRuns()
    {
        // This codebase has shipped five jobs that were never registered and therefore never ran.
        using var scope = _factory.Services.CreateScope();
        scope.ServiceProvider.GetService<ExpireServedSuspensionsJob>().Should().NotBeNull();

        scope.ServiceProvider.GetRequiredService<Application.Common.Interfaces.IBackgroundJobService>()
            .GetRecurringJobIds().Should().Contain("expire-served-suspensions");
    }
}
