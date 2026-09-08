using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
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
/// </summary>
[Collection("Integration")]
public sealed class DuplicateSubscriptionRefundTests
{
    private readonly ApiFactory _factory;

    public DuplicateSubscriptionRefundTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task DuplicateSubscriptionPayment_RaisesRealRefundRequestForTheOwner()
    {
        int packageId, paymentId;
        string orderId;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var package = new SubscriptionPackage
            {
                Name = $"DupPkg-{Guid.NewGuid():N}"[..20],
                Price = 500_000m,
                BillingCycle = SubscriptionBillingCycle.Monthly,
                MaxTicketsPerEvent = 100,
                IsActive = true
            };
            db.Add(package);
            await db.SaveChangesAsync();
            packageId = package.Id;

            // The owner is ALREADY subscribed — that is the state this second callback lands in, and
            // it comes from the shared seed. Deliberately NOT inserting a second OwnerSubscription
            // here: owner_subscriptions has a unique index on OwnerId, which is itself the DB-level
            // half of this defence and would reject the insert outright.
            (await db.OwnerSubscriptions.AnyAsync(
                sub => sub.OwnerId == SeedHelper.OwnerId && sub.Status == SubscriptionStatus.Active))
                .Should().BeTrue("test premise: the owner already holds an active subscription");

            orderId = $"DUP-{Guid.NewGuid():N}"[..30];
            var payment = new Payment
            {
                OrderId = orderId,
                PayerId = SeedHelper.OwnerId,
                GrossAmount = 500_000m,
                Status = PaymentStatus.Pending,
                ReferenceType = "Subscription",
                ReferenceId = packageId.ToString(),
                CreatedAt = DateTimeOffset.UtcNow
            };
            db.Payments.Add(payment);
            await db.SaveChangesAsync();
            paymentId = payment.Id;
        }

        var client = _factory.CreateClient();
        await client.GetAsync(
            $"/api/v1/subscriptions/vnpay-return?vnp_TxnRef={orderId}" +
            $"&vnp_ResponseCode=00&vnp_Amount={500_000L * 100}");

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var payments = await verifyDb.Payments.SingleAsync(p => p.Id == paymentId);
        payments.Status.Should().Be(PaymentStatus.Confirmed,
            "VNPay really did collect this money — booking it is correct, it is the refund that was missing");

        var refunds = await verifyDb.RefundRequests.Where(r => r.PaymentId == paymentId).ToListAsync();
        refunds.Should().HaveCount(1,
            "a duplicate charge must become a real refund request, not just a sentence in a notification");
        refunds[0].Status.Should().Be(RefundRequestStatus.Pending);
        refunds[0].RequestedBy.Should().Be(SeedHelper.OwnerId);
        refunds[0].AmountRequested.Should().Be(500_000m);

        // Exactly one subscription — the duplicate must not have granted a second entitlement.
        var subs = await verifyDb.OwnerSubscriptions
            .CountAsync(s => s.OwnerId == SeedHelper.OwnerId && s.Status == SubscriptionStatus.Active);
        subs.Should().Be(1, "the duplicate payment must not have granted a second entitlement");
    }
}
