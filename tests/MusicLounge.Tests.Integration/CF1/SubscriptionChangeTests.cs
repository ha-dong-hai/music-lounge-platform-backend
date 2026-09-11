using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Hangfire;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Infrastructure.Jobs;
using MusicLounge.Infrastructure.Persistence;
using MusicLounge.Tests.Integration.Helpers;

namespace MusicLounge.Tests.Integration.CF1;

/// <summary>
/// MLACP-371. Huỷ gói từng có hiệu lực NGAY — mất quyền lợi tức thì, không hoàn — và đó là cách duy nhất để đổi
/// sang gói khác, nên đổi gói nghĩa là mất trắng số ngày đã trả. Gia hạn bị chặn khi gói còn hạn.
///
/// <para>Nay (quyết định đã chốt): huỷ = không gia hạn nữa, gói vẫn dùng tới hết kỳ (Stripe
/// cancel_at_period_end); đổi gói có hiệu lực ngay, phần còn lại của gói cũ quy thành thời gian ở gói mới (Stripe
/// proration, không hoàn tiền mặt); gia hạn sớm cộng dồn vào hạn hiện tại (Google Play gói trả trước).</para>
///
/// <para>Số tiền đã trả (<c>AmountPaid</c>) được đọc qua <c>Entry(...).Property</c> chứ không qua thuộc tính, và
/// loại thông báo mới được so theo tên — để file này vẫn biên dịch được trên code cũ và bước "đỏ trước khi sửa"
/// chạy được.</para>
/// </summary>
[Collection("Integration")]
public sealed class SubscriptionChangeTests
{
    private readonly ApiFactory _factory;

    public SubscriptionChangeTests(ApiFactory factory) => _factory = factory;

    private sealed record Wrapped<T>(T Data);

    private sealed record Initiation(int PaymentId, string OrderId, decimal Amount, string PaymentUrl);

    private sealed record ChangeInitiation(
        int PaymentId, string OrderId, decimal Amount, string PaymentUrl,
        decimal CreditValue, decimal CreditDays, DateTimeOffset EstimatedExpiresAt);

    private HttpClient Owner(int ownerId) => _factory.CreateAuthenticatedClient(ownerId, "Owner");

    private async Task<int> FreshOwnerAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var owner = new User { Email = $"plan371-{Guid.NewGuid():N}@test.com", FullName = "Plan Owner" };
        db.Users.Add(owner);
        await db.SaveChangesAsync();
        return owner.Id;
    }

    private async Task<int> PackageAsync(decimal price)
    {
        var res = await _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin").PostAsJsonAsync(
            "/api/v1/subscriptions/packages", new
            {
                Name = $"Pkg371-{Guid.NewGuid():N}", Description = "MLACP-371", Price = price,
                BillingCycle = "Monthly", MaxTicketsPerEvent = 100, HasAiPoster = false, MaxAiPostersPerMonth = 0
            });
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<Wrapped<int>>())!.Data;
    }

    // vnpay-return trả 302 về trang của người mua — như SubscriptionTests, không đi theo chuyển hướng; hiệu lực của
    // thanh toán được kiểm ở trạng thái gói ngay sau đó.
    private async Task PayAsync(string orderId, decimal amount)
        => (await _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }).GetAsync(
                $"/api/v1/subscriptions/vnpay-return?vnp_TxnRef={orderId}&vnp_ResponseCode=00&vnp_Amount={(long)(amount * 100)}"))
            .StatusCode.Should().Be(HttpStatusCode.Redirect);

    private async Task<int> SubscribedOwnerAsync(int packageId, decimal price)
    {
        var ownerId = await FreshOwnerAsync();
        var res = await Owner(ownerId).PostAsJsonAsync("/api/v1/subscriptions/subscribe", new { PackageId = packageId });
        res.StatusCode.Should().Be(HttpStatusCode.Created);
        await PayAsync((await res.Content.ReadFromJsonAsync<Wrapped<Initiation>>())!.Data.OrderId, price);
        return ownerId;
    }

    private async Task<List<OwnerSubscription>> PlansAsync(int ownerId)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>()
            .OwnerSubscriptions.AsNoTracking().Where(s => s.OwnerId == ownerId).ToListAsync();
    }

    private async Task<decimal?> AmountPaidAsync(int subscriptionId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var plan = await db.OwnerSubscriptions.SingleAsync(s => s.Id == subscriptionId);
        return db.Entry(plan).Property<decimal?>("AmountPaid").CurrentValue;
    }

    /// <summary>Đẩy gói về đúng giữa kỳ: đã dùng 15 ngày, còn 15 ngày.</summary>
    private async Task PutPlanHalfwayAsync(int ownerId, bool forgetAmountPaid = false)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var plan = await db.OwnerSubscriptions.SingleAsync(s => s.OwnerId == ownerId && s.Status == SubscriptionStatus.Active);
        plan.StartedAt = DateTimeOffset.UtcNow.AddDays(-15);
        plan.ExpiresAt = DateTimeOffset.UtcNow.AddDays(15);
        if (forgetAmountPaid)
            db.Entry(plan).Property<decimal?>("AmountPaid").CurrentValue = null;
        await db.SaveChangesAsync();
    }

    private async Task<bool> AnyRefundRequestedByAsync(int ownerId)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>()
            .RefundRequests.AnyAsync(r => r.RequestedBy == ownerId);
    }

    // ─── Huỷ: dùng tới hết kỳ ────────────────────────────────────────────────

    [Fact]
    public async Task ACancelledPlan_StaysUsableUntilItsEnd_AndIsNotRefunded()
    {
        var ownerId = await SubscribedOwnerAsync(await PackageAsync(300_000m), 300_000m);

        (await Owner(ownerId).PostAsync("/api/v1/subscriptions/cancel", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var plan = (await PlansAsync(ownerId)).Single();
        plan.Status.Should().Be(SubscriptionStatus.Active, "the owner paid for this period — it runs to its end");
        plan.CancelledAt.Should().NotBeNull();

        var my = await (await Owner(ownerId).GetAsync("/api/v1/subscriptions/my")).Content.ReadAsStringAsync();
        my.Should().Contain("\"status\":\"Active\"").And.Contain("\"cancelledAt\":\"",
            "the owner must be able to see that the plan is cancelled and when it stops");
        (await AnyRefundRequestedByAsync(ownerId)).Should().BeFalse("cancelling does not refund the remaining period");
    }

    [Fact]
    public async Task ACancelledPlan_EndsAsCancelled_WhenItsTimeRunsOut()
    {
        var packageId = await PackageAsync(300_000m);
        var cancelledOwner = await SubscribedOwnerAsync(packageId, 300_000m);
        var keptOwner = await SubscribedOwnerAsync(packageId, 300_000m);
        (await Owner(cancelledOwner).PostAsync("/api/v1/subscriptions/cancel", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            foreach (var plan in await db.OwnerSubscriptions.Where(s => s.OwnerId == cancelledOwner || s.OwnerId == keptOwner).ToListAsync())
                plan.ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1);
            await db.SaveChangesAsync();
        }
        using (var scope = _factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<ExpireSubscriptionsJob>().ExecuteAsync(new JobCancellationToken(false));

        (await PlansAsync(cancelledOwner)).Single().Status.Should().Be(SubscriptionStatus.Cancelled);
        (await PlansAsync(keptOwner)).Single().Status.Should().Be(SubscriptionStatus.Expired);
    }

    // ─── Gia hạn sớm: cộng dồn ───────────────────────────────────────────────

    [Fact]
    public async Task RenewingEarly_AddsAFullCycleOnTopOfTheCurrentEnd_AndUndoesTheCancel()
    {
        var ownerId = await SubscribedOwnerAsync(await PackageAsync(250_000m), 250_000m);
        var before = (await PlansAsync(ownerId)).Single();
        (await AmountPaidAsync(before.Id)).Should().Be(250_000m, "what was paid is recorded when the plan starts");
        (await Owner(ownerId).PostAsync("/api/v1/subscriptions/cancel", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var renew = await Owner(ownerId).PostAsync("/api/v1/subscriptions/renew", null);
        renew.StatusCode.Should().Be(HttpStatusCode.Created, "renewing while the plan still runs is allowed");
        await PayAsync((await renew.Content.ReadFromJsonAsync<Wrapped<Initiation>>())!.Data.OrderId, 250_000m);

        var plan = (await PlansAsync(ownerId)).Single();
        plan.Id.Should().Be(before.Id, "the same plan is extended — no second plan, no refund");
        plan.Status.Should().Be(SubscriptionStatus.Active);
        plan.ExpiresAt.Should().BeCloseTo(before.ExpiresAt.AddMonths(1), TimeSpan.FromMinutes(1),
            "not a single remaining day is lost — the new month starts where the old one ends");
        plan.CancelledAt.Should().BeNull("renewing takes back the cancellation");
        (await AmountPaidAsync(plan.Id)).Should().Be(500_000m);
        (await AnyRefundRequestedByAsync(ownerId)).Should().BeFalse("a renewal is not a duplicate charge");
    }

    [Fact]
    public async Task RenewingTwiceBeforePaying_IsRefused()
    {
        var ownerId = await SubscribedOwnerAsync(await PackageAsync(250_000m), 250_000m);

        (await Owner(ownerId).PostAsync("/api/v1/subscriptions/renew", null)).StatusCode.Should().Be(HttpStatusCode.Created);
        (await Owner(ownerId).PostAsync("/api/v1/subscriptions/renew", null)).StatusCode.Should().Be(HttpStatusCode.Conflict,
            "a double click must not turn into two paid renewals");
    }

    // ─── Đổi gói: hiệu lực ngay, quy đổi phần còn lại ─────────────────────────

    [Fact]
    public async Task ChangingPackage_TakesEffectNow_AndCreditsWhatWasLeftOfTheOldOne()
    {
        var oldPackage = await PackageAsync(300_000m);
        var newPackage = await PackageAsync(600_000m);
        var ownerId = await SubscribedOwnerAsync(oldPackage, 300_000m);
        await PutPlanHalfwayAsync(ownerId);

        var res = await Owner(ownerId).PostAsJsonAsync("/api/v1/subscriptions/change-package", new { PackageId = newPackage });
        res.StatusCode.Should().Be(HttpStatusCode.Created);
        var change = (await res.Content.ReadFromJsonAsync<Wrapped<ChangeInitiation>>())!.Data;
        change.CreditValue.Should().BeApproximately(150_000m, 500m, "half of what was paid is still unused");
        change.CreditDays.Should().BeInRange(6.5m, 8m, "150.000đ is a quarter of the new 600.000đ month");

        var now = DateTimeOffset.UtcNow;
        await PayAsync(change.OrderId, 600_000m);

        var plans = await PlansAsync(ownerId);
        plans.Single(p => p.PackageId == oldPackage).Status.Should().Be(SubscriptionStatus.Cancelled);
        var current = plans.Single(p => p.Status == SubscriptionStatus.Active);
        current.PackageId.Should().Be(newPackage, "the new package takes effect immediately");
        var month = now.AddMonths(1) - now;
        current.ExpiresAt.Should().BeCloseTo(now + month + month / 4, TimeSpan.FromHours(2),
            "a full new month plus the old plan's remaining value, converted at the new price");
        (await AmountPaidAsync(current.Id)).Should().BeApproximately(750_000m, 500m);
        (await AnyRefundRequestedByAsync(ownerId)).Should().BeFalse("a package change is not a duplicate charge");

        using var scope = _factory.Services.CreateScope();
        (await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Notifications.AsNoTracking()
                .Where(n => n.UserId == ownerId).ToListAsync())
            .Any(n => n.Type.ToString() == "SubscriptionUpdated")
            .Should().BeTrue("the owner is told the new end date and what was carried over");
    }

    [Fact]
    public async Task ChangingToTheSamePackage_IsRefused_RenewInstead()
    {
        var packageId = await PackageAsync(300_000m);
        var ownerId = await SubscribedOwnerAsync(packageId, 300_000m);

        var res = await Owner(ownerId).PostAsJsonAsync("/api/v1/subscriptions/change-package", new { PackageId = packageId });

        res.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await res.Content.ReadAsStringAsync()).Should().Contain("Gia hạn");
    }

    [Fact]
    public async Task ChangingWithoutAPlan_IsRefused()
    {
        var ownerId = await FreshOwnerAsync();

        (await Owner(ownerId).PostAsJsonAsync("/api/v1/subscriptions/change-package", new { PackageId = await PackageAsync(300_000m) }))
            .StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task SubscribingAgainWhileActive_PointsToRenewOrChange()
    {
        var ownerId = await SubscribedOwnerAsync(await PackageAsync(300_000m), 300_000m);

        var res = await Owner(ownerId).PostAsJsonAsync("/api/v1/subscriptions/subscribe", new { PackageId = await PackageAsync(400_000m) });

        res.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await res.Content.ReadAsStringAsync()).Should().Contain("Đổi gói");
    }

    [Fact]
    public async Task APlanFromBeforeAmountPaidWasRecorded_IsValuedAtItsListPrice()
    {
        var oldPackage = await PackageAsync(300_000m);
        var ownerId = await SubscribedOwnerAsync(oldPackage, 300_000m);
        await PutPlanHalfwayAsync(ownerId, forgetAmountPaid: true);

        var res = await Owner(ownerId).PostAsJsonAsync("/api/v1/subscriptions/change-package",
            new { PackageId = await PackageAsync(600_000m) });

        res.StatusCode.Should().Be(HttpStatusCode.Created);
        (await res.Content.ReadFromJsonAsync<Wrapped<ChangeInitiation>>())!.Data.CreditValue
            .Should().BeApproximately(150_000m, 500m, "without a recorded amount, the package's list price stands in");
    }
}
