using FluentAssertions;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Infrastructure.Jobs;
using MusicLounge.Tests.Integration.Helpers;

namespace MusicLounge.Tests.Integration.CF7;

/// <summary>
/// master-backend-techlead review — proactive sweep after finding the same SQLite-translation bug
/// four separate times (Lounges, Recommendations, TicketTransferExpiry): every job/query combining
/// an equality or Contains predicate with a DateTimeOffset comparison in one Where clause had zero
/// test coverage. Rather than assume every remaining zero-coverage instance is ALSO broken (some
/// similarly-shaped queries elsewhere in this codebase demonstrably do translate fine — this SQLite
/// provider limitation is inconsistent, not a blanket rule), this test suite actually exercises
/// each one to get a real answer instead of guessing from the pattern alone.
/// </summary>
[Collection("Integration")]
public sealed class JobQueryTranslationTests
{
    private readonly ApiFactory _factory;

    public JobQueryTranslationTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task CancelAbandonedPaymentsJob_RunsWithoutThrowing_AndCancelsStalePendingPayment()
    {
        Guid ticketId;
        int paymentId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var payment = new Payment
            {
                OrderId = $"STALE-{Guid.NewGuid():N}"[..30], GrossAmount = 100_000m,
                Status = PaymentStatus.Pending, ReferenceType = "TicketHold", ReferenceId = "0",
                CreatedAt = DateTimeOffset.UtcNow.AddHours(-1) // older than the 30-min abandon window
            };
            db.Add(payment);
            await db.SaveChangesAsync();
            paymentId = payment.Id;

            var ticket = new Ticket
            {
                Id = Guid.NewGuid(), BuyerId = SeedHelper.AudienceId, PriceId = SeedHelper.TicketPriceId,
                TierId = SeedHelper.TicketTierId, ShowId = SeedHelper.ShowId, PaymentId = payment.Id,
                Status = TicketStatus.Pending, PurchaseChannel = PurchaseChannel.Online,
                CreatedAt = DateTimeOffset.UtcNow
            };
            db.Add(ticket);
            await db.SaveChangesAsync();
            ticketId = ticket.Id;
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var job = scope.ServiceProvider.GetRequiredService<CancelAbandonedPaymentsJob>();
            var act = () => job.ExecuteAsync(new JobCancellationToken(false));
            await act.Should().NotThrowAsync();
        }

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await verifyDb.Payments.FindAsync(paymentId))!.Status.Should().Be(PaymentStatus.Failed);
        (await verifyDb.Tickets.FindAsync(ticketId))!.Status.Should().Be(TicketStatus.Cancelled);
    }

    [Fact]
    public async Task ExpireStuckDonationsJob_RunsWithoutThrowing_AndCancelsStaleDonation()
    {
        int donationId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var donation = new Donation
            {
                DonorUserId = SeedHelper.AudienceId, PerformanceId = SeedHelper.PerformanceId,
                Gross = 50_000m, Net = 45_000m, Status = DonationStatus.PendingPayment,
                GatewayRef = $"STUCK-{Guid.NewGuid():N}", CreatedAt = DateTimeOffset.UtcNow.AddHours(-3)
            };
            db.Add(donation);
            await db.SaveChangesAsync();
            donationId = donation.Id;
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var job = scope.ServiceProvider.GetRequiredService<ExpireStuckDonationsJob>();
            var act = () => job.ExecuteAsync(new JobCancellationToken(false));
            await act.Should().NotThrowAsync();
        }

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await verifyDb.Donations.FindAsync(donationId))!.Status.Should().Be(DonationStatus.Cancelled);
    }

    [Fact]
    public async Task ExpireSubscriptionsJob_RunsWithoutThrowing_AndExpiresPastDueSubscription()
    {
        int subId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var package = new SubscriptionPackage
            {
                Name = $"ExpireTestPkg-{Guid.NewGuid():N}"[..20], Price = 200_000m,
                BillingCycle = SubscriptionBillingCycle.Monthly, MaxTicketsPerEvent = 50,
                HasAiPoster = false, IsActive = true, CreatedAt = DateTimeOffset.UtcNow
            };
            db.Add(package);
            await db.SaveChangesAsync();

            // Own User, not SeedHelper.OwnerId/OtherOwnerId — both already carry a baseline Active
            // subscription from SeedHelper, and OwnerSubscriptionConfiguration now enforces at most
            // 1 Active subscription per owner (D14-adjacent fix), so reusing either would collide.
            var owner = new User { Email = $"expiretest-{Guid.NewGuid():N}@test.com", FullName = "ExpireTestOwner" };
            db.Add(owner);
            await db.SaveChangesAsync();

            var sub = new OwnerSubscription
            {
                OwnerId = owner.Id, PackageId = package.Id,
                StartedAt = DateTimeOffset.UtcNow.AddDays(-31), ExpiresAt = DateTimeOffset.UtcNow.AddDays(-1),
                Status = SubscriptionStatus.Active, MaxTicketsPerEventSnapshot = 50, HasAiPosterSnapshot = false
            };
            db.Add(sub);
            await db.SaveChangesAsync();
            subId = sub.Id;
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var job = scope.ServiceProvider.GetRequiredService<ExpireSubscriptionsJob>();
            var act = () => job.ExecuteAsync(new JobCancellationToken(false));
            await act.Should().NotThrowAsync();
        }

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await verifyDb.OwnerSubscriptions.FindAsync(subId))!.Status.Should().Be(SubscriptionStatus.Expired);
    }

    [Fact]
    public async Task SubscriptionExpiryWarningJob_RunsWithoutThrowing()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var package = new SubscriptionPackage
            {
                Name = $"WarnTestPkg-{Guid.NewGuid():N}"[..20], Price = 200_000m,
                BillingCycle = SubscriptionBillingCycle.Monthly, MaxTicketsPerEvent = 50,
                HasAiPoster = false, IsActive = true, CreatedAt = DateTimeOffset.UtcNow
            };
            db.Add(package);
            await db.SaveChangesAsync();

            // Own User, not SeedHelper.OwnerId/OtherOwnerId — see ExpireSubscriptionsJob test above
            // for why reusing either now collides with OwnerSubscriptionConfiguration's per-owner
            // Active uniqueness constraint.
            var owner = new User { Email = $"warntest-{Guid.NewGuid():N}@test.com", FullName = "WarnTestOwner" };
            db.Add(owner);
            await db.SaveChangesAsync();

            db.Add(new OwnerSubscription
            {
                OwnerId = owner.Id, PackageId = package.Id,
                StartedAt = DateTimeOffset.UtcNow.AddDays(-23), ExpiresAt = DateTimeOffset.UtcNow.AddDays(7),
                Status = SubscriptionStatus.Active, MaxTicketsPerEventSnapshot = 50, HasAiPosterSnapshot = false
            });
            await db.SaveChangesAsync();
        }

        using var jobScope = _factory.Services.CreateScope();
        var job = jobScope.ServiceProvider.GetRequiredService<SubscriptionExpiryWarningJob>();
        var act = () => job.ExecuteAsync(new JobCancellationToken(false));
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task AutoConfirmDonationsJob_RunsWithoutThrowing_AndAutoConfirmsOverdueDonation()
    {
        int donationId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var donation = new Donation
            {
                DonorUserId = SeedHelper.AudienceId, PerformanceId = SeedHelper.PerformanceId,
                Gross = 50_000m, Net = 45_000m, Status = DonationStatus.PendingOwnerAck,
                GatewayRef = $"AUTOCONFIRM-{Guid.NewGuid():N}",
                PaymentConfirmedAt = DateTimeOffset.UtcNow.AddHours(-25) // past the 24h auto-confirm window
            };
            db.Add(donation);
            await db.SaveChangesAsync();
            donationId = donation.Id;
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var job = scope.ServiceProvider.GetRequiredService<AutoConfirmDonationsJob>();
            var act = () => job.ExecuteAsync(new JobCancellationToken(false));
            await act.Should().NotThrowAsync();
        }

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var updated = (await verifyDb.Donations.FindAsync(donationId))!;
        updated.Status.Should().Be(DonationStatus.OwnerReceived);
        updated.AutoConfirmed.Should().BeTrue();
    }

    [Fact]
    public async Task DonationOverdueCheckJob_RunsWithoutThrowing_AndRemindsOwnerOfOverdueDonation()
    {
        int donationId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var donation = new Donation
            {
                DonorUserId = SeedHelper.AudienceId, PerformanceId = SeedHelper.PerformanceId,
                Gross = 50_000m, Net = 45_000m, Status = DonationStatus.OwnerReceived,
                GatewayRef = $"OVERDUE-{Guid.NewGuid():N}",
                OwnerAckAt = DateTimeOffset.UtcNow.AddDays(-8) // past the 7-day reminder threshold
            };
            db.Add(donation);
            await db.SaveChangesAsync();
            donationId = donation.Id;
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var job = scope.ServiceProvider.GetRequiredService<DonationOverdueCheckJob>();
            var act = () => job.ExecuteAsync(new JobCancellationToken(false));
            await act.Should().NotThrowAsync();
        }

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await verifyDb.Notifications.AnyAsync(n =>
                n.UserId == SeedHelper.OwnerId
                && n.Type == NotificationType.DonationPending
                && n.ReferenceId == donationId.ToString()))
            .Should().BeTrue();
    }

    [Fact]
    public async Task EventReminderJob_RunsWithoutThrowing()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var show = new LoungeShow
            {
                LoungeId = SeedHelper.LoungeId, Name = $"ReminderTestShow-{Guid.NewGuid():N}",
                Description = "test", Format = LoungeShowFormat.Offline, Status = LoungeShowStatus.Published,
                ScheduledStart = DateTimeOffset.UtcNow.AddHours(12) // within the default 24h reminder window
            };
            db.Add(show);
            await db.SaveChangesAsync();

            db.Add(new Ticket
            {
                Id = Guid.NewGuid(), BuyerId = SeedHelper.AudienceId, PriceId = SeedHelper.TicketPriceId,
                TierId = SeedHelper.TicketTierId, ShowId = show.Id, Status = TicketStatus.Confirmed,
                PurchaseChannel = PurchaseChannel.Online, CreatedAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();
        }

        using var jobScope = _factory.Services.CreateScope();
        var job = jobScope.ServiceProvider.GetRequiredService<EventReminderJob>();
        var act = () => job.ExecuteAsync(new JobCancellationToken(false));
        await act.Should().NotThrowAsync();
    }
}
