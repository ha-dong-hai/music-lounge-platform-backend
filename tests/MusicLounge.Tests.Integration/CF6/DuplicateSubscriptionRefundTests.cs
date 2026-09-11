using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Infrastructure.Jobs;
using MusicLounge.Infrastructure.Persistence;
using MusicLounge.Tests.Integration.Helpers;

namespace MusicLounge.Tests.Integration.CF6;

/// <summary>
/// Đợt-8 audit. A double-submitted Subscribe can leave VNPay having genuinely collected a second
/// payment for an entitlement the owner already has. That case was already detected, booked as a
/// ledger liability described "needs refund", and the owner was told the money "sẽ được xem xét
/// hoàn lại" — but no RefundRequest was ever created, so the promise existed only in a notification
/// string and a ledger description. Nothing put it in the Admin queue, and nothing chased it.
///
/// Same shape as the ComplaintResolvedAction.Refund no-op fixed in đợt 6: the system said money
/// would move and then nothing did.
///
/// <para>MLACP-366. The request was then created — but it could never be APPROVED: the refund handler
/// looked for a venue owner through a ticket or an F&amp;B order, a plan payment has neither, and every
/// attempt (Admin or AutoApproveOverdueRefundsJob) ended in a 422. The first test only proved the
/// request existed, which is why nobody noticed.</para>
/// </summary>
[Collection("Integration")]
public sealed class DuplicateSubscriptionRefundTests
{
    private const decimal Price = 500_000m;

    private readonly ApiFactory _factory;

    public DuplicateSubscriptionRefundTests(ApiFactory factory) => _factory = factory;

    /// <summary>
    /// The seeded owner ALREADY holds an Active plan and pays for another one; VNPay confirms it.
    /// </summary>
    private async Task<(int PaymentId, int RefundId)> DuplicatePaymentAsync()
    {
        int paymentId;
        string orderId;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var package = new SubscriptionPackage
            {
                Name = $"DupPkg-{Guid.NewGuid():N}"[..20],
                Price = Price,
                BillingCycle = SubscriptionBillingCycle.Monthly,
                MaxTicketsPerEvent = 100,
                IsActive = true
            };
            db.Add(package);
            await db.SaveChangesAsync();

            // Deliberately NOT inserting a second OwnerSubscription here: owner_subscriptions has a
            // unique index on OwnerId, which is itself the DB-level half of this defence and would
            // reject the insert outright.
            (await db.OwnerSubscriptions.AnyAsync(
                sub => sub.OwnerId == SeedHelper.OwnerId && sub.Status == SubscriptionStatus.Active))
                .Should().BeTrue("test premise: the owner already holds an active subscription");

            orderId = $"DUP-{Guid.NewGuid():N}"[..30];
            var payment = new Payment
            {
                OrderId = orderId,
                PayerId = SeedHelper.OwnerId,
                GrossAmount = Price,
                Status = PaymentStatus.Pending,
                ReferenceType = "Subscription",
                ReferenceId = package.Id.ToString(),
                CreatedAt = DateTimeOffset.UtcNow
            };
            db.Payments.Add(payment);
            await db.SaveChangesAsync();
            paymentId = payment.Id;
        }

        await _factory.CreateClient().GetAsync(
            $"/api/v1/subscriptions/vnpay-return?vnp_TxnRef={orderId}" +
            $"&vnp_ResponseCode=00&vnp_Amount={(long)(Price * 100)}");

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var refund = await verifyDb.RefundRequests.SingleAsync(r => r.PaymentId == paymentId);
        return (paymentId, refund.Id);
    }

    [Fact]
    public async Task DuplicateSubscriptionPayment_RaisesRealRefundRequestForTheOwner()
    {
        var (paymentId, refundId) = await DuplicatePaymentAsync();

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var payments = await verifyDb.Payments.SingleAsync(p => p.Id == paymentId);
        payments.Status.Should().Be(PaymentStatus.Confirmed,
            "VNPay really did collect this money — booking it is correct, it is the refund that was missing");

        var refunds = await verifyDb.RefundRequests.Where(r => r.PaymentId == paymentId).ToListAsync();
        refunds.Should().HaveCount(1,
            "a duplicate charge must become a real refund request, not just a sentence in a notification");
        refunds[0].Id.Should().Be(refundId);
        refunds[0].Status.Should().Be(RefundRequestStatus.Pending);
        refunds[0].RequestedBy.Should().Be(SeedHelper.OwnerId);
        refunds[0].AmountRequested.Should().Be(Price);

        // Exactly one subscription — the duplicate must not have granted a second entitlement.
        var subs = await verifyDb.OwnerSubscriptions
            .CountAsync(s => s.OwnerId == SeedHelper.OwnerId && s.Status == SubscriptionStatus.Active);
        subs.Should().Be(1, "the duplicate payment must not have granted a second entitlement");
    }

    [Fact]
    public async Task DuplicateSubscriptionRefund_CanBeApproved_AndReversesPlatformRevenue_NotTheOwnersAccount()
    {
        var (paymentId, refundId) = await DuplicatePaymentAsync();

        (await _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin").PostAsJsonAsync(
                $"/api/v1/admin/refund-requests/{refundId}/process", new { Decision = "Approved" }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent,
                "a plan is the platform's own revenue — there is no venue owner in between to look up");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        (await db.RefundRequests.SingleAsync(r => r.Id == refundId)).Status.Should().Be(RefundRequestStatus.Approved);
        (await db.Payments.SingleAsync(p => p.Id == paymentId)).Status.Should().Be(PaymentStatus.Refunded);

        var reversal = await db.LedgerEntries
            .Include(e => e.Account)
            .Where(e => e.PaymentId == paymentId && e.ReferenceType == "refund")
            .ToListAsync();
        reversal.Select(e => (e.Account.OwnerType, e.IsDebit, e.Amount)).Should().BeEquivalentTo(
            new[] { (AccountType.Platform, true, Price), (AccountType.Gateway, false, Price) },
            "the reversal mirrors the original Gateway-debit / Platform-credit journal — the money sits " +
            "with the platform, never in the owner's own account");

        (await db.Notifications.AnyAsync(n =>
                n.UserId == SeedHelper.OwnerId && n.Type == NotificationType.RefundUpdate
                && n.ReferenceId == refundId.ToString()))
            .Should().BeTrue("the owner who paid twice must be told the money is on its way back");
    }

    [Fact]
    public async Task OverdueDuplicateSubscriptionRefund_IsApprovedByTheJob_InsteadOfFailingEveryRun()
    {
        var (_, refundId) = await DuplicatePaymentAsync();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var refund = await db.RefundRequests.SingleAsync(r => r.Id == refundId);
            refund.CreatedAt = DateTime.UtcNow.AddDays(-30);
            await db.SaveChangesAsync();
        }

        using (var scope = _factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<AutoApproveOverdueRefundsJob>()
                .ExecuteAsync(new JobCancellationToken(false));

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            (await db.RefundRequests.SingleAsync(r => r.Id == refundId)).Status
                .Should().Be(RefundRequestStatus.Approved,
                    "past the SLA the job approves it — before, it failed on every run and only logged");
        }
    }
}
