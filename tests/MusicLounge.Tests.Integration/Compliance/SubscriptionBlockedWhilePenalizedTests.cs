using System.Net;
using System.Net.Http.Json;
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
/// MLACP-376. Chủ có phòng trà đang <c>Locked</c>/<c>Suspended</c> vẫn mua, gia hạn, đổi gói dịch vụ thành
/// công (phát hiện qua kiểm thử runtime 2026-09-12: trả 300.000đ, gói chuyển <c>Active</c>) dù mọi hành động
/// cần tới gói (tạo buổi diễn, bán vé, nhận donation) đều đã bị <c>VenueLifecycle.CanOperate</c> chặn ở cấp
/// phòng trà (MLACP-354) — nền tảng tự thu tiền cho một dịch vụ không dùng được ngay.
///
/// <para>Không chặn <c>Pending</c>/<c>Rejected</c> (chưa từng được duyệt, không phải hình phạt) và chủ
/// <b>chưa có phòng trà nào</b> (MLACP-374: tối đa một phòng trà) — cả hai đều là luồng mua trước hợp lệ.
/// <c>Warned</c> vẫn hoạt động bình thường nên cũng không bị chặn.</para>
/// </summary>
[Collection("Integration")]
public sealed class SubscriptionBlockedWhilePenalizedTests
{
    private readonly ApiFactory _factory;

    public SubscriptionBlockedWhilePenalizedTests(ApiFactory factory) => _factory = factory;

    private sealed record Wrapped<T>(T Data);

    private sealed record Initiation(int PaymentId, string OrderId, decimal Amount, string PaymentUrl);

    private HttpClient Owner(int ownerId) => _factory.CreateAuthenticatedClient(ownerId, "Owner");

    private async Task<int> FreshOwnerAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var owner = new User { Email = $"pen376-{Guid.NewGuid():N}@test.com", FullName = "Penalized Owner" };
        db.Users.Add(owner);
        await db.SaveChangesAsync();
        return owner.Id;
    }

    private async Task SeedLoungeAsync(int ownerId, LoungeStatus status)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Lounges.Add(new MusicLoungeVenue
        {
            OwnerId = ownerId, Name = $"Venue376-{Guid.NewGuid():N}"[..30], Status = status,
            Address = new VenueAddress { Street = "1 Test St", District = "1", City = "HCM" }
        });
        await db.SaveChangesAsync();
    }

    private async Task<int> PackageAsync(decimal price = 300_000m)
    {
        var res = await _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin").PostAsJsonAsync(
            "/api/v1/subscriptions/packages", new
            {
                Name = $"Pkg376-{Guid.NewGuid():N}", Description = "MLACP-376", Price = price,
                BillingCycle = "Monthly", MaxTicketsPerEvent = 100, HasAiPoster = false, MaxAiPostersPerMonth = 0
            });
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<Wrapped<int>>())!.Data;
    }

    /// <summary>Seed một gói Active trực tiếp — dùng cho Renew/ChangePackage mà không cần trả tiền thật.</summary>
    private async Task SeedActivePlanAsync(int ownerId, int packageId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.OwnerSubscriptions.Add(new OwnerSubscription
        {
            OwnerId = ownerId, PackageId = packageId, StartedAt = DateTimeOffset.UtcNow.AddDays(-1),
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(29), Status = SubscriptionStatus.Active,
            AmountPaid = 300_000m, MaxTicketsPerEventSnapshot = 100
        });
        await db.SaveChangesAsync();
    }

    // ── Bị chặn: Suspended / Locked ────────────────────────────────────────

    [Theory]
    [InlineData(LoungeStatus.Locked)]
    [InlineData(LoungeStatus.Suspended)]
    public async Task Subscribe_WhileVenueIsPenalized_Returns409(LoungeStatus status)
    {
        var ownerId = await FreshOwnerAsync();
        await SeedLoungeAsync(ownerId, status);
        var packageId = await PackageAsync();

        var res = await Owner(ownerId).PostAsJsonAsync("/api/v1/subscriptions/subscribe", new { PackageId = packageId });

        res.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var body = await res.Content.ReadAsStringAsync();
        body.Should().Contain(status == LoungeStatus.Locked ? "khoá vĩnh viễn" : "tạm khoá");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.OwnerSubscriptions.AnyAsync(s => s.OwnerId == ownerId)).Should().BeFalse(
            "yêu cầu bị chặn thì không được tạo thanh toán nào");
    }

    [Fact]
    public async Task Renew_WhileVenueIsSuspended_Returns409()
    {
        var ownerId = await FreshOwnerAsync();
        var packageId = await PackageAsync();
        await SeedActivePlanAsync(ownerId, packageId);
        await SeedLoungeAsync(ownerId, LoungeStatus.Suspended);

        var res = await Owner(ownerId).PostAsync("/api/v1/subscriptions/renew", null);

        res.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task ChangePackage_WhileVenueIsLocked_Returns409()
    {
        var ownerId = await FreshOwnerAsync();
        var packageId = await PackageAsync();
        var otherPackageId = await PackageAsync(600_000m);
        await SeedActivePlanAsync(ownerId, packageId);
        await SeedLoungeAsync(ownerId, LoungeStatus.Locked);

        var res = await Owner(ownerId).PostAsJsonAsync("/api/v1/subscriptions/change-package", new { PackageId = otherPackageId });

        res.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // ── Không bị chặn: chưa có phòng trà, Pending, Rejected, Warned ─────────

    [Fact]
    public async Task Subscribe_WithNoLoungeYet_Succeeds()
    {
        var ownerId = await FreshOwnerAsync();
        var packageId = await PackageAsync();

        var res = await Owner(ownerId).PostAsJsonAsync("/api/v1/subscriptions/subscribe", new { PackageId = packageId });

        res.StatusCode.Should().Be(HttpStatusCode.Created, "chủ mới đăng ký gói trước khi tạo phòng trà là luồng hợp lệ");
    }

    [Theory]
    [InlineData(LoungeStatus.Pending)]
    [InlineData(LoungeStatus.Rejected)]
    [InlineData(LoungeStatus.Warned)]
    public async Task Subscribe_WhileVenueIsNotPenalized_Succeeds(LoungeStatus status)
    {
        var ownerId = await FreshOwnerAsync();
        await SeedLoungeAsync(ownerId, status);
        var packageId = await PackageAsync();

        var res = await Owner(ownerId).PostAsJsonAsync("/api/v1/subscriptions/subscribe", new { PackageId = packageId });

        res.StatusCode.Should().Be(HttpStatusCode.Created,
            $"{status} không phải hình phạt — không được chặn giống Suspended/Locked");
    }
}
