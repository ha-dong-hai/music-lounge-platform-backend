using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Hangfire;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MusicLounge.Application.Tickets.Events;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Infrastructure.Jobs;
using MusicLounge.Infrastructure.Persistence;
using MusicLounge.Tests.Integration.Helpers;

namespace MusicLounge.Tests.Integration.CF6;

/// <summary>
/// CF6 W27/W28 — Settlement release job + ledger integrity reconciliation
/// </summary>
[Collection("Integration")]
public sealed class SettlementTests
{
    private readonly ApiFactory _factory;

    public SettlementTests(ApiFactory factory) => _factory = factory;

    private async Task<(int PaymentId, int SettlementId)> SeedDueSettlementAsync(
        SettlementReleaseType releaseType = SettlementReleaseType.Partial70)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var payment = new Payment
        {
            OrderId = $"SET-{Guid.NewGuid():N}"[..30],
            GrossAmount = 1_000_000m,
            Status = PaymentStatus.Confirmed,
            ReferenceType = "TicketHold",
            ReferenceId = "0",
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.Add(payment);
        await db.SaveChangesAsync();

        var settlement = new Settlement
        {
            OwnerId = SeedHelper.OwnerId,
            PaymentId = payment.Id,
            ReleaseType = releaseType,
            GrossAmount = 1_000_000m,
            PreRateApplied = 0.70m,
            PostRateApplied = 0.30m,
            NetAmount = 700_000m,
            Status = SettlementStatus.Scheduled,
            ScheduledAt = DateTimeOffset.UtcNow.AddDays(-1), // already due
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.Add(settlement);
        await db.SaveChangesAsync();

        return (payment.Id, settlement.Id);
    }

    /// <summary>
    /// Regression test for a double-credit defect found in this session's audit: the payment
    /// journal (WriteTicketLedgerHandler) used to credit the owner's own User account with the
    /// full net amount immediately, and SettlementReleaseJob later credited that same account
    /// again for the 70% + 30% settlement tranches derived from that same net amount — paying
    /// every owner exactly double. Drives the real production handlers end-to-end (domain event
    /// → ledger + settlement creation → settlement release) rather than seeding a Settlement row
    /// directly, because seeding around WriteTicketLedgerHandler/ScheduleSettlementHandler is
    /// exactly how the original bug went unnoticed by every other test in this file.
    /// </summary>
    [Fact]
    public async Task TicketPurchaseThroughSettlementRelease_CreditsOwnerNetExactlyOnce()
    {
        int paymentId;
        Guid ticketId;
        int showId;
        const decimal gross = 1_000_000m;
        const decimal expectedOwnerNet = 900_000m; // gross - 5% platform - 5% tax (system_config defaults)

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var show = new LoungeShow
            {
                LoungeId = SeedHelper.LoungeId,
                Name = $"SettlementE2EShow-{Guid.NewGuid():N}",
                Description = "Integration test show",
                Format = LoungeShowFormat.Offline,
                Status = LoungeShowStatus.Ended,
                ScheduledStart = DateTimeOffset.UtcNow.AddDays(-5),
                ScheduledEnd = DateTimeOffset.UtcNow.AddDays(-5).AddHours(3)
            };
            db.LoungeShows.Add(show);
            await db.SaveChangesAsync();
            showId = show.Id;

            var payment = new Payment
            {
                OrderId = $"E2E-{Guid.NewGuid():N}"[..30],
                GrossAmount = gross,
                Status = PaymentStatus.Confirmed,
                ReferenceType = "TicketHold", ReferenceId = "0",
                PaidAt = DateTimeOffset.UtcNow, CreatedAt = DateTimeOffset.UtcNow
            };
            db.Payments.Add(payment);
            await db.SaveChangesAsync();
            paymentId = payment.Id;

            var ticket = new Ticket
            {
                Id = Guid.NewGuid(),
                BuyerId = SeedHelper.AudienceId,
                PriceId = SeedHelper.TicketPriceId,
                TierId = SeedHelper.TicketTierId,
                ShowId = show.Id,
                PaymentId = payment.Id,
                Status = TicketStatus.Confirmed,
                PurchaseChannel = PurchaseChannel.Online,
                CreatedAt = DateTimeOffset.UtcNow
            };
            db.Tickets.Add(ticket);
            await db.SaveChangesAsync();
            ticketId = ticket.Id;
        }

        // Fire the same domain event PurchaseTicket/ProcessVnPayCallback publish on confirmation —
        // triggers WriteTicketLedgerHandler + ScheduleSettlementHandler exactly as production does.
        using (var scope = _factory.Services.CreateScope())
        {
            var publisher = scope.ServiceProvider.GetRequiredService<IPublisher>();
            await publisher.Publish(new TicketPaymentConfirmed(
                PaymentId: paymentId, UserId: SeedHelper.AudienceId, OwnerId: SeedHelper.OwnerId,
                TicketIds: [ticketId], LivestreamId: null, ShowId: showId));
        }

        // Force both settlement tranches due, then run the real release job.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var settlements = await db.Settlements.Where(s => s.PaymentId == paymentId).ToListAsync();
            settlements.Should().HaveCount(2, "ScheduleSettlementHandler always creates Partial70 + Final30");
            foreach (var s in settlements) s.ScheduledAt = DateTimeOffset.UtcNow.AddMinutes(-1);
            await db.SaveChangesAsync();
        }
        using (var scope = _factory.Services.CreateScope())
        {
            var job = scope.ServiceProvider.GetRequiredService<SettlementReleaseJob>();
            await job.ExecuteAsync(new JobCancellationToken(false));
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var ownerAccount = await db.LedgerAccounts.SingleOrDefaultAsync(
                a => a.OwnerType == AccountType.User && a.OwnerId == SeedHelper.OwnerId);
            ownerAccount.Should().NotBeNull();

            var ownerEntries = await db.LedgerEntries
                .Where(e => e.PaymentId == paymentId && e.AccountId == ownerAccount!.Id)
                .ToListAsync();
            var netCredited = ownerEntries.Where(e => !e.IsDebit).Sum(e => e.Amount)
                             - ownerEntries.Where(e => e.IsDebit).Sum(e => e.Amount);

            netCredited.Should().Be(expectedOwnerNet,
                "chủ phòng trà chỉ được ghi có đúng 1 lần cho mỗi khoản, không phải 2 lần " +
                "(1 lần lúc thanh toán + 1 lần lúc settlement release)");
        }
    }

    /// <summary>
    /// Regression test for a gap found in this session's entity/business-completeness audit:
    /// WriteTicketLedgerHandler existed on local master but was never ported to origin, so every
    /// VNPay ticket purchase confirmed silently without ever writing the "payment" journal (Gateway
    /// debit / Platform+Tax credit) or populating Payment.PlatformFee/TaxWithheld/NetAmount — the
    /// existing end-to-end test above never caught this because it only asserts the owner's
    /// eventual settlement-release credit, which ScheduleSettlementHandler computes independently
    /// and would still land correctly even with zero ledger entries for the original sale.
    /// </summary>
    [Fact]
    public async Task TicketPaymentConfirmed_WritesBalancedPaymentJournal_AndPopulatesPaymentFeeBreakdown()
    {
        const decimal gross = 1_000_000m;
        const decimal expectedPlatformFee = 50_000m; // 5% system_config default
        const decimal expectedTax = 50_000m;         // 5% system_config default
        const decimal expectedOwnerNet = 900_000m;

        int paymentId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var payment = new Payment
            {
                OrderId = $"LEDGER-{Guid.NewGuid():N}"[..30],
                GrossAmount = gross,
                Status = PaymentStatus.Confirmed,
                ReferenceType = "TicketHold", ReferenceId = "0",
                PaidAt = DateTimeOffset.UtcNow, CreatedAt = DateTimeOffset.UtcNow
            };
            db.Payments.Add(payment);
            await db.SaveChangesAsync();
            paymentId = payment.Id;
        }

        // OwnerId: 0 — deliberately isolates WriteTicketLedgerHandler under test: it's the exact
        // value ScheduleSettlementHandler itself treats as "nothing to schedule" and returns early
        // on (see its own `if (payment is null || notification.OwnerId == 0) return;`), so this
        // doesn't also require seeding a ticket/show/lounge/bank-account just to reach the
        // assertions below. WriteTicketLedgerHandler only interpolates OwnerId into a ledger line's
        // free-text Description, never validates it.
        using (var scope = _factory.Services.CreateScope())
        {
            var publisher = scope.ServiceProvider.GetRequiredService<IPublisher>();
            await publisher.Publish(new TicketPaymentConfirmed(
                PaymentId: paymentId, UserId: SeedHelper.AudienceId, OwnerId: 0,
                TicketIds: [], LivestreamId: null, ShowId: SeedHelper.ShowId));
        }

        using var scope2 = _factory.Services.CreateScope();
        var verifyDb = scope2.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var payment2 = await verifyDb.Payments.SingleAsync(p => p.Id == paymentId);
        payment2.PlatformFee.Should().Be(expectedPlatformFee,
            "H1 — fee breakdown must snapshot onto the Payment row at confirmation time");
        payment2.TaxWithheld.Should().Be(expectedTax);
        payment2.NetAmount.Should().Be(expectedOwnerNet);

        var entries = await verifyDb.LedgerEntries.Where(e => e.PaymentId == paymentId).ToListAsync();
        entries.Should().NotBeEmpty(
            "confirming a ticket payment must write the Gateway/Platform/Tax journal — a payment " +
            "with zero ledger entries is invisible to any accounting/audit trail");
        entries.Where(e => e.IsDebit).Sum(e => e.Amount)
            .Should().Be(entries.Where(e => !e.IsDebit).Sum(e => e.Amount),
                "D8 double-entry invariant — every journal must balance");
        entries.Where(e => e.IsDebit).Sum(e => e.Amount).Should().Be(gross);
    }

    [Fact]
    public async Task SettlementReleaseJob_ReleasesDueSettlement_AndWritesBalancedLedgerJournal()
    {
        var (_, settlementId) = await SeedDueSettlementAsync();

        using (var scope = _factory.Services.CreateScope())
        {
            var job = scope.ServiceProvider.GetRequiredService<SettlementReleaseJob>();
            await job.ExecuteAsync(new JobCancellationToken(false));
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var settlement = await db.Settlements.FindAsync(settlementId);

            settlement!.Status.Should().Be(SettlementStatus.Released);
            settlement.ReleasedAt.Should().NotBeNull();
            settlement.LedgerJournalId.Should().NotBeNullOrEmpty();

            var entries = db.LedgerEntries.Where(e => e.JournalId == settlement.LedgerJournalId).ToList();
            entries.Should().HaveCount(2);
            entries.Where(e => e.IsDebit).Sum(e => e.Amount)
                .Should().Be(entries.Where(e => !e.IsDebit).Sum(e => e.Amount));
        }
    }

    [Fact]
    public async Task SettlementReleaseJob_LeavesNotYetDueSettlementUntouched()
    {
        int settlementId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var payment = new Payment
            {
                OrderId = $"SET-{Guid.NewGuid():N}"[..30],
                GrossAmount = 500_000m,
                Status = PaymentStatus.Confirmed,
                ReferenceType = "TicketHold",
                ReferenceId = "0",
                CreatedAt = DateTimeOffset.UtcNow
            };
            db.Add(payment);
            await db.SaveChangesAsync();

            var settlement = new Settlement
            {
                OwnerId = SeedHelper.OwnerId,
                PaymentId = payment.Id,
                ReleaseType = SettlementReleaseType.Partial70,
                GrossAmount = 500_000m,
                PreRateApplied = 0.70m,
                PostRateApplied = 0.30m,
                NetAmount = 350_000m,
                Status = SettlementStatus.Scheduled,
                ScheduledAt = DateTimeOffset.UtcNow.AddDays(5), // not due yet
                CreatedAt = DateTimeOffset.UtcNow
            };
            db.Add(settlement);
            await db.SaveChangesAsync();
            settlementId = settlement.Id;
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var job = scope.ServiceProvider.GetRequiredService<SettlementReleaseJob>();
            await job.ExecuteAsync(new JobCancellationToken(false));
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var settlement = await db.Settlements.FindAsync(settlementId);
            settlement!.Status.Should().Be(SettlementStatus.Scheduled);
        }
    }

    // ─── W28 Ledger integrity ──────────────────────────────────────────────────

    [Fact]
    public async Task LedgerIntegrityCheck_AsAdmin_ReturnsEmptyWhenBalanced()
    {
        var (_, settlementId) = await SeedDueSettlementAsync();
        using (var scope = _factory.Services.CreateScope())
        {
            var job = scope.ServiceProvider.GetRequiredService<SettlementReleaseJob>();
            await job.ExecuteAsync(new JobCancellationToken(false));
        }

        var client = _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin");
        var res = await client.GetAsync("/api/v1/admin/ledger/integrity-check");

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await res.Content.ReadAsStringAsync();
        body.Should().Contain("\"data\":[]");
    }

    [Fact]
    public async Task LedgerIntegrityCheck_AsNonAdmin_Returns403()
    {
        var client = _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner");

        var res = await client.GetAsync("/api/v1/admin/ledger/integrity-check");

        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
