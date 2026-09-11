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
/// MLACP-375. Ngày được <b>bù miễn phí</b> khi tạm khoá oan (<c>ApplyDuePenaltiesJob</c> cộng thẳng
/// <c>SuspensionDays</c> vào <c>ExpiresAt</c>, không cộng gì vào <c>AmountPaid</c>) từng bị
/// <c>SubscriptionTerms.RemainingValue</c> tính như ngày đã trả tiền khi đổi gói giữa kỳ.
///
/// <para>Đo được (kiểm thử runtime 2026-09-12): gói 300.000đ/30 ngày còn 5 ngày (giá trị đúng
/// 50.000đ). Sau khi được bù 10 ngày, công thức cũ trả về 112.500đ — quy đổi được nhiều hơn
/// 62.500đ so với số tiền thật đã trả cho phần chưa dùng. Thiệt hại thuộc về nền tảng.</para>
///
/// <para>Không thể sửa bằng cách trừ thẳng "tổng ngày được bù" khỏi phần còn lại: nếu chủ
/// <b>gia hạn sau khi được bù</b>, ngày miễn phí không còn nằm cuối kỳ nữa — trừ mù sẽ tính THIẾU
/// giá trị cho chủ. Bài <c>CreditAfterSuspensionThenRenewal_StillReflectsOnlyPaidTime</c> ghim đúng
/// trường hợp phân biệt hai cách sửa này.</para>
/// </summary>
[Collection("Integration")]
public sealed class CompensationAwareCreditTests
{
    private readonly ApiFactory _factory;

    public CompensationAwareCreditTests(ApiFactory factory) => _factory = factory;

    private sealed record Wrapped<T>(T Data);

    private sealed record ChangeInitiation(
        int PaymentId, string OrderId, decimal Amount, string PaymentUrl,
        decimal CreditValue, decimal CreditDays, DateTimeOffset EstimatedExpiresAt);

    private HttpClient Owner(int ownerId) => _factory.CreateAuthenticatedClient(ownerId, "Owner");

    private async Task<(int OwnerId, int LoungeId)> FreshOwnerWithLoungeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var owner = new User { Email = $"credit375-{Guid.NewGuid():N}@test.com", FullName = "Credit Owner" };
        db.Users.Add(owner);
        await db.SaveChangesAsync();
        var lounge = new MusicLoungeVenue
        {
            OwnerId = owner.Id, Name = $"Venue375-{Guid.NewGuid():N}"[..30], Status = LoungeStatus.Approved,
            Address = new VenueAddress { Street = "1 Test St", District = "1", City = "HCM" }
        };
        db.Lounges.Add(lounge);
        await db.SaveChangesAsync();
        return (owner.Id, lounge.Id);
    }

    private async Task<int> PackageAsync(decimal price)
    {
        var res = await _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin").PostAsJsonAsync(
            "/api/v1/subscriptions/packages", new
            {
                Name = $"Pkg375-{Guid.NewGuid():N}", Description = "MLACP-375", Price = price,
                BillingCycle = "Monthly", MaxTicketsPerEvent = 100, HasAiPoster = false, MaxAiPostersPerMonth = 0
            });
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<Wrapped<int>>())!.Data;
    }

    /// <summary>Seed thẳng một gói Active — kiểm soát chính xác StartedAt/ExpiresAt/AmountPaid cho phép tính.</summary>
    private async Task<int> SeedActivePlanAsync(int ownerId, int packageId, DateTimeOffset startedAt, DateTimeOffset expiresAt, decimal amountPaid)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var plan = new OwnerSubscription
        {
            OwnerId = ownerId, PackageId = packageId, StartedAt = startedAt, ExpiresAt = expiresAt,
            Status = SubscriptionStatus.Active, AmountPaid = amountPaid, MaxTicketsPerEventSnapshot = 100
        };
        db.OwnerSubscriptions.Add(plan);
        await db.SaveChangesAsync();
        return plan.Id;
    }

    private async Task ApplySuspensionAsync(int loungeId, int suspensionDays)
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.VenuePenalties.Add(new VenuePenalty
            {
                LoungeId = loungeId, PenaltyType = PenaltyType.Suspension, Reason = "MLACP-375 test",
                IssuedBy = SeedHelper.AdminId, IssuedAt = DateTimeOffset.UtcNow.AddDays(-1),
                EffectiveAt = DateTimeOffset.UtcNow.AddMinutes(-1), SuspensionDays = suspensionDays,
                Status = PenaltyStatus.Active
            });
            await db.SaveChangesAsync();
        }
        using var jobScope = _factory.Services.CreateScope();
        await jobScope.ServiceProvider.GetRequiredService<ApplyDuePenaltiesJob>()
            .ExecuteAsync(new JobCancellationToken(false));

        // MLACP-376: doi gói bị chặn khi phòng trà đang Suspended/Locked (trả tiền cho dịch vụ không dùng
        // được). Bài này đo CÔNG THỨC quy đổi có tính đúng phần được bù hay không, không phải đo hành vi lúc
        // đang bị khoá — mô phỏng "hạn tạm khoá đã qua, venue hoạt động lại" trong khi phần bù vẫn còn nguyên
        // trên VenuePenalty (ExpiresAt đã cộng, SubscriptionCompensationDays đã ghi ở bước job trên).
        using (var restoreScope = _factory.Services.CreateScope())
        {
            var db = restoreScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var lounge = await db.Lounges.SingleAsync(l => l.Id == loungeId);
            lounge.Status = LoungeStatus.Approved;
            await db.SaveChangesAsync();
        }
    }

    // Bai nay chi so sanh SO TIEN QUY DOI truoc/sau — khong tra tien lan nao, nen lenh doi goi truoc do (neu
    // co) van con Pending mai. Don no truoc, giong het CancelAbandonedPaymentsJob (Failed), de moi lan goi
    // deu doc lap voi lich su goi truoc.
    private async Task<ChangeInitiation> ChangePackageAsync(int ownerId, int newPackageId)
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var stale = await db.Payments.Where(p => p.PayerId == ownerId
                && p.ReferenceType == "Subscription" && p.Status == PaymentStatus.Pending).ToListAsync();
            foreach (var p in stale) p.Status = PaymentStatus.Failed;
            await db.SaveChangesAsync();
        }

        var res = await Owner(ownerId).PostAsJsonAsync("/api/v1/subscriptions/change-package", new { PackageId = newPackageId });
        var text = await res.Content.ReadAsStringAsync();
        res.StatusCode.Should().Be(HttpStatusCode.Created, text);
        return (await res.Content.ReadFromJsonAsync<Wrapped<ChangeInitiation>>())!.Data;
    }

    [Fact]
    public async Task CreditAfterSuspension_DoesNotCountTheFreeDaysAsPaid()
    {
        var (ownerId, loungeId) = await FreshOwnerWithLoungeAsync();
        var oldPackage = await PackageAsync(300_000m);
        var newPackage = await PackageAsync(600_000m);
        var now = DateTimeOffset.UtcNow;
        // 30 ngay da tra, con 5 ngay chua dung (gia tri dung = 300000 * 5/30 = 50000).
        await SeedActivePlanAsync(ownerId, oldPackage, now.AddDays(-25), now.AddDays(5), 300_000m);

        var before = await ChangePackageAsync(ownerId, newPackage);
        before.CreditValue.Should().BeApproximately(50_000m, 500m,
            "chưa bị tạm khoá — công thức cũ và mới phải khớp nhau ở đây");

        await ApplySuspensionAsync(loungeId, suspensionDays: 10);

        var after = await ChangePackageAsync(ownerId, newPackage);
        after.CreditValue.Should().BeApproximately(50_000m, 500m,
            "10 ngày được bù không phải ngày đã trả tiền — được bù thêm thời gian dùng, không được thêm giá trị quy đổi");
    }

    [Fact]
    public async Task CreditAfterSuspensionThenRenewal_StillReflectsOnlyPaidTime()
    {
        // Chu gia han SAU KHI duoc bu: ngay mien phi (10 ngay, dang o cuoi ky truoc khi gia han) khong con
        // nam cuoi cung nua — mot cong thuc tru mu "tong ngay duoc bu" khoi phan con lai se tinh THIEU gia
        // tri cho chu o day. Phai dung dung KHOANG THOI GIAN da ghi tren VenuePenalty.
        var (ownerId, loungeId) = await FreshOwnerWithLoungeAsync();
        var oldPackage = await PackageAsync(300_000m);
        var newPackage = await PackageAsync(600_000m);
        var now = DateTimeOffset.UtcNow;
        var planId = await SeedActivePlanAsync(ownerId, oldPackage, now.AddDays(-20), now.AddDays(10), 300_000m);

        await ApplySuspensionAsync(loungeId, suspensionDays: 10); // ExpiresAt: +10 -> now+20; vung mien phi = [now+10, now+20]

        // Gia han: cong mot ky day du (30 ngay) noi vao ExpiresAt hien tai (da bao gom 10 ngay mien phi).
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var plan = await db.OwnerSubscriptions.SingleAsync(s => s.Id == planId);
            plan.ExpiresAt = plan.ExpiresAt.AddDays(30); // now+50
            plan.AmountPaid += 300_000m; // 600_000m tong
            await db.SaveChangesAsync();
        }

        // Tai thoi diem "now": da qua het vung mien phi [now+10, now+20] tu lau? Khong — "now" la thoi diem
        // hien tai, vung mien phi con o TUONG LAI (now+10 den now+20), vi vay no van la mot phan cua "left".
        // Tong con lai (left) = 50 ngay; trong do 10 ngay la mien phi (nam giua) -> 40 ngay co gia tri that.
        // Tong ca ky (total) = StartedAt den ExpiresAt = 20+50 = 70 ngay; tru 10 ngay mien phi = 60 ngay co gia
        // tri. AmountPaid = 600000. Gia tri dung = 600000 * 40/60 = 400000.
        var result = await ChangePackageAsync(ownerId, newPackage);
        result.CreditValue.Should().BeApproximately(400_000m, 1_000m,
            "ngày miễn phí nằm GIỮA kỳ (trước phần gia hạn), không phải cuối kỳ — công thức phải theo đúng " +
            "khoảng thời gian đã ghi, không phải phép trừ vị trí cố định");
    }

    [Fact]
    public async Task ABanRestoredOntoADifferentActivePlan_DoesNotInflateThatPlansCredit()
    {
        // PenaltySubscriptions.RestoreAfterBanLifted nhanh gop: khi lenh khoa duoc huy, phan con lai cua goi
        // CU (da tra tien cho goi do) duoc cong vao ExpiresAt cua mot goi KHAC dang Active — ma khong cong gi
        // vao AmountPaid cua goi do. Neu khong ghi lai, no pha loang gia tri quy doi cua goi dang Active y het
        // mot lan bu tam khoa.
        var (ownerId, loungeId) = await FreshOwnerWithLoungeAsync();
        var bannedPackage = await PackageAsync(300_000m);
        var activePackage = await PackageAsync(300_000m);
        var changePackage = await PackageAsync(600_000m);
        var now = DateTimeOffset.UtcNow;

        // Goi bi khoa: con 12 ngay luc bi khoa (se duoc tra lai nguyen 12 ngay nay).
        var appliedAt = now.AddDays(-3);
        var bannedPlanId = await SeedActivePlanAsync(ownerId, bannedPackage, now.AddDays(-18), appliedAt.AddDays(12), 300_000m);
        int banPenaltyId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var banned = await db.OwnerSubscriptions.SingleAsync(s => s.Id == bannedPlanId);
            banned.Status = SubscriptionStatus.Cancelled;
            banned.CancelledAt = appliedAt;
            var ban = new VenuePenalty
            {
                LoungeId = loungeId, PenaltyType = PenaltyType.Ban, Reason = "MLACP-375 test",
                IssuedBy = SeedHelper.AdminId, IssuedAt = appliedAt.AddDays(-1), EffectiveAt = appliedAt,
                AppliedAt = appliedAt, Status = PenaltyStatus.Appealed, AppealedAt = now.AddHours(-1)
            };
            db.VenuePenalties.Add(ban);
            await db.SaveChangesAsync();
            banPenaltyId = ban.Id;
        }

        // Chu da mua mot goi KHAC (Active) trong luc bi khoa/khang cao — con 20 ngay, gia tri dung 200000.
        var activePlanId = await SeedActivePlanAsync(ownerId, activePackage, now.AddDays(-10), now.AddDays(20), 300_000m);

        var before = await ChangePackageAsync(ownerId, changePackage);
        before.CreditValue.Should().BeApproximately(200_000m, 500m);

        var overturn = await _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin").PostAsJsonAsync(
            $"/api/v1/venue-penalties/{banPenaltyId}/appeal/review", new { Decision = "Overturned", ReviewNote = "Test" });
        overturn.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var active = await db.OwnerSubscriptions.SingleAsync(s => s.Id == activePlanId);
            active.ExpiresAt.Should().BeCloseTo(now.AddDays(32), TimeSpan.FromMinutes(2),
                "12 ngày còn lại của gói bị khoá được gộp vào gói đang Active");
        }

        var after = await ChangePackageAsync(ownerId, changePackage);
        after.CreditValue.Should().BeApproximately(200_000m, 500m,
            "12 ngày gộp thêm không phải tiền trả cho gói này — không được làm giá trị quy đổi tăng lên");
    }
}
