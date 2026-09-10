using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Infrastructure.Persistence;
using MusicLounge.Tests.Integration.Helpers;

namespace MusicLounge.Tests.Integration.CF6;

/// <summary>
/// Đợt-4 audit. At purchase, the owner's share is credited to AccountType.Platform (held in trust);
/// each SettlementReleaseJob tranche then moves it to the owner's own AccountType.User. The refund
/// reversal must debit whichever account still actually holds the money.
///
/// It used to always debit Platform, on the stated assumption that a refund could never land after
/// a tranche released — "CancelTicket only allows cancellation before show.ScheduledStart, while the
/// earliest tranche fires at showEnd+48h". That assumption does not hold:
///   - CancellationDeadlineHours is optional (MLACP-257), so there may be no deadline at all;
///   - CancelTicket's only hard stop is show.Status == Ended;
///   - nothing in the system ever ends a show automatically — an offline show with no livestream
///     stays Published forever unless the Owner remembers to press "End";
///   - both tranches release anyway, Final30 included, because its completion check returns true
///     precisely when ActualStart/ActualEnd are null.
/// So a ticket stays cancellable weeks after the money has already been paid out to the venue.
/// </summary>
[Collection("Integration")]
public sealed class RefundAfterSettlementTests
{
    private readonly ApiFactory _factory;

    public RefundAfterSettlementTests(ApiFactory factory) => _factory = factory;

    private const decimal Gross = 1_000_000m;
    private const decimal PlatformFee = 50_000m;   // 5% (system_config default)
    private const decimal Tax = 50_000m;           // 5%
    private const decimal OwnerNet = 900_000m;

    /// <summary>Payment whose settlement has ALREADY been released to the owner, plus a pending refund.</summary>
    private async Task<int> SeedFullyReleasedPaymentWithPendingRefundAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var show = new LoungeShow
        {
            LoungeId = SeedHelper.LoungeId,
            Name = $"NeverEndedShow-{Guid.NewGuid():N}",
            Description = "Owner never pressed End, so this is still Published",
            Format = LoungeShowFormat.Offline,
            Status = LoungeShowStatus.Published,
            ScheduledStart = DateTimeOffset.UtcNow.AddDays(-20),
            ScheduledEnd = DateTimeOffset.UtcNow.AddDays(-20).AddHours(3)
        };
        db.LoungeShows.Add(show);
        await db.SaveChangesAsync();

        var payment = new Payment
        {
            OrderId = $"RAS-{Guid.NewGuid():N}"[..30],
            GrossAmount = Gross,
            PlatformFee = PlatformFee,
            TaxWithheld = Tax,
            NetAmount = OwnerNet,
            Method = PaymentMethod.Gateway,
            Status = PaymentStatus.Confirmed,
            TransactionId = $"RAS{Guid.NewGuid():N}"[..16],
            ReferenceType = "TicketHold",
            ReferenceId = "0",
            PaidAt = DateTimeOffset.UtcNow.AddDays(-21),
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-21)
        };
        db.Payments.Add(payment);
        await db.SaveChangesAsync();

        db.Tickets.Add(new Ticket
        {
            Id = Guid.NewGuid(),
            BuyerId = SeedHelper.AudienceId,
            PriceId = SeedHelper.TicketPriceId,
            TierId = SeedHelper.TicketTierId,
            ShowId = show.Id,
            PaymentId = payment.Id,
            Status = TicketStatus.Confirmed,
            PurchaseChannel = PurchaseChannel.Online,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-21)
        });

        // Both tranches already paid out — the owner's 900,000đ now sits in their User account,
        // not in Platform.
        db.Settlements.Add(new Settlement
        {
            OwnerId = SeedHelper.OwnerId, PaymentId = payment.Id,
            ReleaseType = SettlementReleaseType.Partial70,
            GrossAmount = Gross, PreRateApplied = 0.70m, PostRateApplied = 0.30m,
            NetAmount = 630_000m, Status = SettlementStatus.Released,
            ReleasedAt = DateTimeOffset.UtcNow.AddDays(-18),
            ScheduledAt = DateTimeOffset.UtcNow.AddDays(-18), CreatedAt = DateTimeOffset.UtcNow.AddDays(-21)
        });
        db.Settlements.Add(new Settlement
        {
            OwnerId = SeedHelper.OwnerId, PaymentId = payment.Id,
            ReleaseType = SettlementReleaseType.Final30,
            GrossAmount = Gross, PreRateApplied = 0.70m, PostRateApplied = 0.30m,
            NetAmount = 270_000m, Status = SettlementStatus.Released,
            ReleasedAt = DateTimeOffset.UtcNow.AddDays(-6),
            ScheduledAt = DateTimeOffset.UtcNow.AddDays(-6), CreatedAt = DateTimeOffset.UtcNow.AddDays(-21)
        });

        var refund = new RefundRequest
        {
            PaymentId = payment.Id,
            RequestedBy = SeedHelper.AudienceId,
            Reason = "Khách huỷ vé sau khi show đã diễn (show chưa bao giờ được đánh dấu Ended)",
            AmountRequested = Gross,
            RefundPercentage = 100m,
            Status = RefundRequestStatus.Pending
        };
        db.RefundRequests.Add(refund);
        await db.SaveChangesAsync();

        return refund.Id;
    }

    [Fact]
    public async Task ApproveRefund_AfterSettlementAlreadyReleased_ClawsBackFromOwnerNotPlatform()
    {
        var refundId = await SeedFullyReleasedPaymentWithPendingRefundAsync();
        var adminClient = _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin");

        var res = await adminClient.PostAsJsonAsync(
            $"/api/v1/admin/refund-requests/{refundId}/process",
            new { Decision = "Approved", ApprovedAmount = (decimal?)null });
        res.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var refund = await db.RefundRequests.SingleAsync(r => r.Id == refundId);
        var journalEntries = await db.LedgerEntries
            .Include(e => e.Account)
            .Where(e => e.ReferenceType == LedgerReferenceTypes.Refund
                        && e.ReferenceId == refundId.ToString())
            .ToListAsync();

        journalEntries.Should().NotBeEmpty("the refund must have written a reversing journal");

        var ownerDebit = journalEntries
            .Where(e => e.IsDebit && e.Account.OwnerType == AccountType.User
                        && e.Account.OwnerId == SeedHelper.OwnerId)
            .Sum(e => e.Amount);
        var platformDebit = journalEntries
            .Where(e => e.IsDebit && e.Account.OwnerType == AccountType.Platform)
            .Sum(e => e.Amount);

        ownerDebit.Should().Be(OwnerNet,
            "the whole owner share had already been released, so all of it must be clawed back from " +
            "the owner's own account");
        platformDebit.Should().Be(PlatformFee,
            "Platform must only be debited for the commission it still holds — debiting it for the " +
            "already-paid-out owner share takes money from an account that no longer has it");

        // Double-entry must still balance, whichever account the owner share came from.
        journalEntries.Where(e => e.IsDebit).Sum(e => e.Amount)
            .Should().Be(journalEntries.Where(e => !e.IsDebit).Sum(e => e.Amount));

        refund.Status.Should().Be(RefundRequestStatus.Approved);
    }
}
