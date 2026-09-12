using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.ValueObjects;
using MusicLounge.Infrastructure.Persistence;
using MusicLounge.Tests.Integration.Helpers;
using MusicLoungeVenue = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Tests.Integration.Compliance;

/// <summary>
/// MLACP-386. <c>SubscriptionVenueGate</c> (MLACP-376) chỉ chặn lúc BẮT ĐẦU thanh toán gói. Phòng trà bị tạm khoá /
/// khoá đúng lúc chủ còn trên trang VNPay thì IPN vẫn kích hoạt, gia hạn hay đổi gói — thu tiền cho một gói chủ không
/// dùng được. Shopify: cửa hàng bị đóng băng thì "all billing attempts stop until you reactivate".
///
/// <para>Nay: IPN hỏi lại cùng quy tắc; bị phạt thì không kích hoạt, tự tạo yêu cầu hoàn 100% cho chủ.</para>
/// </summary>
[Collection("Integration")]
public sealed class SubscriptionPaidWhileVenuePenalizedTests
{
    private readonly ApiFactory _factory;

    public SubscriptionPaidWhileVenuePenalizedTests(ApiFactory factory) => _factory = factory;

    private sealed record Wrapped<T>(T Data);
    private sealed record IpnBody(string RspCode, string Message);
    private sealed record Paid(int PaymentId, string OrderId, decimal Amount);

    private HttpClient Owner(int ownerId) => _factory.CreateAuthenticatedClient(ownerId, "Owner");

    private ApplicationDbContext Db(IServiceScope scope) => scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

    /// <summary>Chủ mới + phòng trà đang hoạt động bình thường (lúc bắt đầu trả tiền, chốt MLACP-376 cho qua).</summary>
    private async Task<(int OwnerId, int LoungeId)> OwnerWithVenueAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = Db(scope);
        var owner = new User { Email = $"s386-{Guid.NewGuid():N}@test.com", FullName = "Chủ phòng trà" };
        db.Users.Add(owner);
        await db.SaveChangesAsync();
        var lounge = new MusicLoungeVenue
        {
            OwnerId = owner.Id, Name = $"Venue386-{Guid.NewGuid():N}"[..30], Status = LoungeStatus.Approved,
            Address = new VenueAddress { Street = "1 Test St", District = "1", City = "HCM" }
        };
        db.Lounges.Add(lounge);
        await db.SaveChangesAsync();
        return (owner.Id, lounge.Id);
    }

    private async Task SetVenueStatusAsync(int loungeId, LoungeStatus status)
    {
        using var scope = _factory.Services.CreateScope();
        var db = Db(scope);
        (await db.Lounges.SingleAsync(l => l.Id == loungeId)).Status = status;
        await db.SaveChangesAsync();
    }

    private async Task<int> PackageAsync(decimal price = 300_000m)
    {
        var res = await _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin").PostAsJsonAsync(
            "/api/v1/subscriptions/packages", new
            {
                Name = $"Pkg386-{Guid.NewGuid():N}", Description = "MLACP-386", Price = price,
                BillingCycle = "Monthly", MaxTicketsPerEvent = 100, HasAiPoster = false, MaxAiPostersPerMonth = 0
            });
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<Wrapped<int>>())!.Data;
    }

    private async Task<OwnerSubscription> SeedActivePlanAsync(int ownerId, int packageId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = Db(scope);
        var plan = new OwnerSubscription
        {
            OwnerId = ownerId, PackageId = packageId, StartedAt = DateTimeOffset.UtcNow.AddDays(-1),
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(29), Status = SubscriptionStatus.Active,
            AmountPaid = 300_000m, MaxTicketsPerEventSnapshot = 100
        };
        db.OwnerSubscriptions.Add(plan);
        await db.SaveChangesAsync();
        return plan;
    }

    /// <summary>Đọc mã đơn từ phản hồi khởi tạo; số tiền lấy từ bản ghi thanh toán — đúng số VNPay được yêu cầu thu.</summary>
    private async Task<Paid> PaidFromAsync(HttpResponseMessage initiation)
    {
        initiation.IsSuccessStatusCode.Should().BeTrue(await initiation.Content.ReadAsStringAsync());
        using var json = JsonDocument.Parse(await initiation.Content.ReadAsStringAsync());
        var orderId = json.RootElement.GetProperty("data").GetProperty("orderId").GetString()!;
        using var scope = _factory.Services.CreateScope();
        var payment = await Db(scope).Payments.AsNoTracking().SingleAsync(p => p.OrderId == orderId);
        return new Paid(payment.Id, orderId, payment.GrossAmount);
    }

    private async Task<IpnBody> PaidIpnAsync(Paid paid, string transactionNo)
    {
        var res = await _factory.CreateClient().GetAsync(
            $"/api/v1/subscriptions/vnpay-ipn?vnp_TxnRef={paid.OrderId}&vnp_ResponseCode=00" +
            $"&vnp_Amount={(long)(paid.Amount * 100)}&vnp_TransactionNo={transactionNo}");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await res.Content.ReadFromJsonAsync<IpnBody>())!;
    }

    private static string NewTransactionNo() => $"S{Guid.NewGuid():N}"[..16];

    [Fact]
    public async Task ASubscriptionPaidJustAfterTheVenueWasLocked_IsNotActivated_AndRefundedInFull()
    {
        var (ownerId, loungeId) = await OwnerWithVenueAsync();
        var packageId = await PackageAsync();
        var paid = await PaidFromAsync(
            await Owner(ownerId).PostAsJsonAsync("/api/v1/subscriptions/subscribe", new { PackageId = packageId }));
        await SetVenueStatusAsync(loungeId, LoungeStatus.Locked);
        var transactionNo = NewTransactionNo();

        (await PaidIpnAsync(paid, transactionNo)).RspCode.Should().Be("02");

        using var scope = _factory.Services.CreateScope();
        var db = Db(scope);
        (await db.OwnerSubscriptions.AnyAsync(s => s.OwnerId == ownerId && s.Status == SubscriptionStatus.Active))
            .Should().BeFalse("a locked venue cannot use the plan it was just charged for");
        var payment = await db.Payments.AsNoTracking().SingleAsync(p => p.Id == paid.PaymentId);
        payment.Status.Should().Be(PaymentStatus.Failed, "Failed here means 'not applied to any plan'");
        payment.TransactionId.Should().Be(transactionNo, "the VNPay refund needs the real transaction number");

        var refund = (await db.RefundRequests.AsNoTracking().Where(r => r.PaymentId == paid.PaymentId).ToListAsync())
            .Should().ContainSingle().Subject;
        refund.RefundPercentage.Should().Be(100m);
        refund.AmountRequested.Should().Be(paid.Amount);
        refund.RequestedBy.Should().Be(ownerId);

        (await db.LedgerEntries.AsNoTracking().AnyAsync(e => e.PaymentId == paid.PaymentId))
            .Should().BeFalse("no plan was sold, so no revenue may be booked");
        (await db.Notifications.AsNoTracking()
                .Where(n => n.UserId == ownerId && n.Type == NotificationType.RefundUpdate).ToListAsync())
            .Should().Contain(n => n.Body.Contains(transactionNo) && n.Body.Contains("khoá vĩnh viễn"),
                "the owner is told why the plan did not start and that the money is coming back");
    }

    [Fact]
    public async Task ARenewalPaidJustAfterTheVenueWasSuspended_DoesNotExtendThePlan()
    {
        var (ownerId, loungeId) = await OwnerWithVenueAsync();
        var packageId = await PackageAsync();
        var plan = await SeedActivePlanAsync(ownerId, packageId);
        var paid = await PaidFromAsync(await Owner(ownerId).PostAsync("/api/v1/subscriptions/renew", null));
        await SetVenueStatusAsync(loungeId, LoungeStatus.Suspended);

        (await PaidIpnAsync(paid, NewTransactionNo())).RspCode.Should().Be("02");

        using var scope = _factory.Services.CreateScope();
        var db = Db(scope);
        (await db.OwnerSubscriptions.AsNoTracking().SingleAsync(s => s.Id == plan.Id)).ExpiresAt
            .Should().BeCloseTo(plan.ExpiresAt, TimeSpan.FromSeconds(1), "the renewal must not be applied");
        (await db.RefundRequests.AsNoTracking().Where(r => r.PaymentId == paid.PaymentId).ToListAsync())
            .Should().ContainSingle().Which.RefundPercentage.Should().Be(100m);
    }

    [Fact]
    public async Task ARepeatedCallback_QueuesNoSecondRefund_AndDoesNotAlertAdminsAgain()
    {
        var (ownerId, loungeId) = await OwnerWithVenueAsync();
        var packageId = await PackageAsync();
        var paid = await PaidFromAsync(
            await Owner(ownerId).PostAsJsonAsync("/api/v1/subscriptions/subscribe", new { PackageId = packageId }));
        await SetVenueStatusAsync(loungeId, LoungeStatus.Locked);
        var transactionNo = NewTransactionNo();

        (await PaidIpnAsync(paid, transactionNo)).RspCode.Should().Be("02");
        (await PaidIpnAsync(paid, transactionNo)).RspCode.Should().Be("02");

        using var scope = _factory.Services.CreateScope();
        var db = Db(scope);
        (await db.RefundRequests.AsNoTracking().CountAsync(r => r.PaymentId == paid.PaymentId)).Should().Be(1);
        var alerts = await db.Notifications.AsNoTracking()
            .Where(n => n.Type == NotificationType.PaymentConfirmedAfterExpiry
                        && n.ReferenceType == "payment" && n.ReferenceId == paid.PaymentId.ToString())
            .ToListAsync();
        alerts.Should().NotBeEmpty();
        alerts.GroupBy(n => n.UserId).Should().OnlyContain(g => g.Count() == 1,
            "a replay of an already-recorded incident must not page every Admin again");
    }

    [Fact]
    public async Task TheQueuedRefund_CanActuallyBeApproved()
    {
        var (ownerId, loungeId) = await OwnerWithVenueAsync();
        var packageId = await PackageAsync();
        var paid = await PaidFromAsync(
            await Owner(ownerId).PostAsJsonAsync("/api/v1/subscriptions/subscribe", new { PackageId = packageId }));
        await SetVenueStatusAsync(loungeId, LoungeStatus.Locked);
        await PaidIpnAsync(paid, NewTransactionNo());

        int refundId;
        using (var scope = _factory.Services.CreateScope())
            refundId = (await Db(scope).RefundRequests.AsNoTracking().SingleAsync(r => r.PaymentId == paid.PaymentId)).Id;

        var res = await _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin").PostAsJsonAsync(
            $"/api/v1/admin/refund-requests/{refundId}/process", new { Decision = "Approved", ApprovedAmount = (decimal?)null });

        res.IsSuccessStatusCode.Should().BeTrue(
            "a refund request nobody can process is only a promise — " + await res.Content.ReadAsStringAsync());
        using var check = _factory.Services.CreateScope();
        (await Db(check).Payments.AsNoTracking().SingleAsync(p => p.Id == paid.PaymentId)).Status
            .Should().Be(PaymentStatus.Refunded);
        (await Db(check).LedgerEntries.AsNoTracking().AnyAsync(e => e.PaymentId == paid.PaymentId))
            .Should().BeFalse("reversing revenue that was never booked would debit the platform for money it never held");
    }

    [Theory]
    [InlineData(LoungeStatus.Approved)]
    [InlineData(LoungeStatus.Warned)]
    public async Task VenueStillOperating_ThePlanIsActivatedAsBefore(LoungeStatus status)
    {
        var (ownerId, loungeId) = await OwnerWithVenueAsync();
        var packageId = await PackageAsync();
        var paid = await PaidFromAsync(
            await Owner(ownerId).PostAsJsonAsync("/api/v1/subscriptions/subscribe", new { PackageId = packageId }));
        await SetVenueStatusAsync(loungeId, status);

        (await PaidIpnAsync(paid, NewTransactionNo())).RspCode.Should().Be("00");

        using var scope = _factory.Services.CreateScope();
        (await Db(scope).OwnerSubscriptions.AnyAsync(s => s.OwnerId == ownerId && s.Status == SubscriptionStatus.Active))
            .Should().BeTrue($"{status} is not a penalty — the new check must not block a normal purchase");
    }
}
