using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Infrastructure.Persistence;
using MusicLounge.Tests.Integration.Helpers;
using MusicLoungeVenue = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Tests.Integration.CF1;

/// <summary>
/// W02 Subscription — D12 (package immutability once subscribed), D14 (event-creation gate)
/// POST /api/v1/subscriptions/packages | PUT .../{id} | POST .../subscribe | GET .../vnpay-return | GET .../my
/// </summary>
[Collection("Integration")]
public sealed class SubscriptionTests
{
    private readonly ApiFactory _factory;

    public SubscriptionTests(ApiFactory factory) => _factory = factory;

    // SeedHelper.OwnerId/OtherOwnerId are pre-seeded with an Active subscription (D14 gate for
    // OTHER tests) — subscribe-flow tests need an owner with NO existing subscription instead.
    private static int _freshOwnerCounter = 9100;

    private async Task<Guid> CreateFreshOwnerAsync()
    {
        var id = TestId.Of(Interlocked.Increment(ref _freshOwnerCounter));
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Users.Add(new User { Id = id, Email = $"freshowner{id}@test.com", FullName = "Fresh Owner" });
        await db.SaveChangesAsync();
        return id;
    }

    private async Task<Guid> CreatePackageAsync(decimal price = 500_000m, int maxTicketsPerEvent = 100)
    {
        var client = _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin");
        var res = await client.PostAsJsonAsync("/api/v1/subscriptions/packages", new
        {
            Name = $"Pkg-{Guid.NewGuid():N}",
            Description = "Test package",
            Price = price,
            BillingCycle = "Monthly",
            MaxTicketsPerEvent = maxTicketsPerEvent,
            HasAiPoster = true,
            MaxAiPostersPerMonth = 10
        });
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<IdResponse>();
        return body!.Data;
    }

    [Fact]
    public async Task CreatePackage_AsAdmin_Returns201()
    {
        var client = _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin");

        var res = await client.PostAsJsonAsync("/api/v1/subscriptions/packages", new
        {
            Name = "Pro Monthly",
            Description = "test",
            Price = 300_000m,
            BillingCycle = "Monthly",
            MaxTicketsPerEvent = 50,
            HasAiPoster = false
        });

        res.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    /// <summary>
    /// Regression test for MLACP-253 (audit-flagged D1 gap, 2026-09-04): SubscriptionPackage used
    /// to inherit bare BaseEntity, so there was no record of which Admin created/changed a package
    /// (Price/entitlement edits affect platform revenue). Now inherits AuditableEntity — confirms
    /// the generic ApplicationDbContext.SaveChangesAsync stamp actually fires for this entity.
    /// </summary>
    [Fact]
    public async Task CreatePackage_StampsCreatedByWithAdmin()
    {
        var packageId = await CreatePackageAsync();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var package = await db.SubscriptionPackages.FindAsync(packageId);

        package!.CreatedBy.Should().Be(SeedHelper.AdminId);
        package.CreatedAt.Should().NotBe(default);
    }

    [Fact]
    public async Task CreatePackage_AsNonAdmin_Returns403()
    {
        var client = _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner");

        var res = await client.PostAsJsonAsync("/api/v1/subscriptions/packages", new
        {
            Name = "Pro Monthly", Description = "test", Price = 300_000m,
            BillingCycle = "Monthly", MaxTicketsPerEvent = 50, HasAiPoster = false
        });

        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Subscribe_ThenConfirmViaVnPay_ActivatesSubscription()
    {
        var packageId = await CreatePackageAsync(price: 250_000m);
        var ownerId = await CreateFreshOwnerAsync();
        var ownerClient = _factory.CreateAuthenticatedClient(ownerId, "Owner");

        var subscribeRes = await ownerClient.PostAsJsonAsync(
            "/api/v1/subscriptions/subscribe", new { PackageId = packageId });
        subscribeRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var initiation = await subscribeRes.Content.ReadFromJsonAsync<SubscriptionInitiationResponse>();

        var noRedirectClient = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var callbackRes = await noRedirectClient.GetAsync(
            $"/api/v1/subscriptions/vnpay-return?vnp_TxnRef={initiation!.Data.OrderId}" +
            $"&vnp_ResponseCode=00&vnp_Amount={(long)(initiation.Data.Amount * 100)}");
        callbackRes.StatusCode.Should().Be(HttpStatusCode.Redirect);

        var myRes = await ownerClient.GetAsync("/api/v1/subscriptions/my");
        var body = await myRes.Content.ReadAsStringAsync();
        body.Should().Contain("\"status\":\"Active\"");
    }

    [Fact]
    public async Task CreateLoungeShow_WithoutActiveSubscription_Returns422()
    {
        Guid newOwnerId, newLoungeId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var user = new User { Id = TestId.Of(9001), Email = "nosub-owner@test.com", FullName = "NoSub Owner" };
            db.Users.Add(user);
            var lounge = new MusicLoungeVenue
            {
                Id = TestId.Of(9001), OwnerId = TestId.Of(9001), Name = "NoSub Lounge",
                Address = new MusicLounge.Domain.ValueObjects.VenueAddress
                {
                    Street = "1 Test", District = "1", City = "HCM"
                }
            };
            db.Lounges.Add(lounge);
            await db.SaveChangesAsync();
            newOwnerId = user.Id;
            newLoungeId = lounge.Id;
        }

        var client = _factory.CreateAuthenticatedClient(newOwnerId, "Owner", newLoungeId);
        var res = await client.PostAsJsonAsync("/api/v1/lounge-shows", new
        {
            LoungeId = newLoungeId,
            Name = "No Sub Show",
            Description = "test",
            Format = "Offline",
            ScheduledStart = DateTimeOffset.UtcNow.AddDays(20),
            ScheduledEnd = (DateTimeOffset?)null,
            CategoryId = (Guid?)null,
            OfflineQuota = 50,
            OnlineQuota = (int?)null,
            GenreIds = Array.Empty<Guid>(), MoodIds = Array.Empty<Guid>(), AtmosphereIds = Array.Empty<Guid>(),
            Performances = Array.Empty<object>()
        });

        res.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task CreateLoungeShow_WithActiveSubscription_Returns201()
    {
        // SeedHelper.OwnerId is pre-seeded with an Active subscription (see SeedHelper.cs)
        var client = _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner", SeedHelper.LoungeId);

        var res = await client.PostAsJsonAsync("/api/v1/lounge-shows", new
        {
            LoungeId = SeedHelper.LoungeId,
            Name = "Has Sub Show",
            Description = "test",
            Format = "Offline",
            ScheduledStart = SeedHelper.NextShowStart(),
            ScheduledEnd = (DateTimeOffset?)null,
            CategoryId = (Guid?)null,
            OfflineQuota = 50,
            OnlineQuota = (int?)null,
            GenreIds = Array.Empty<Guid>(), MoodIds = Array.Empty<Guid>(), AtmosphereIds = Array.Empty<Guid>(),
            Performances = Array.Empty<object>()
        });

        res.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task UpdatePackage_PriceChange_BlockedWhenActiveSubscriberExists()
    {
        var packageId = await CreatePackageAsync();
        var ownerId = await CreateFreshOwnerAsync();
        var ownerClient = _factory.CreateAuthenticatedClient(ownerId, "Owner");
        var subscribeRes = await ownerClient.PostAsJsonAsync(
            "/api/v1/subscriptions/subscribe", new { PackageId = packageId });
        var initiation = await subscribeRes.Content.ReadFromJsonAsync<SubscriptionInitiationResponse>();
        await ownerClient.GetAsync(
            $"/api/v1/subscriptions/vnpay-return?vnp_TxnRef={initiation!.Data.OrderId}" +
            $"&vnp_ResponseCode=00&vnp_Amount={(long)(initiation.Data.Amount * 100)}");

        var adminClient = _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin");
        var res = await adminClient.PutAsJsonAsync($"/api/v1/subscriptions/packages/{packageId}", new
        {
            Description = "Updated", Price = 999_999m, MaxTicketsPerEvent = 100, HasAiPoster = true,
            MaxAiPostersPerMonth = 10, IsActive = true
        });

        res.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task UpdatePackage_DescriptionOnly_AlwaysAllowed()
    {
        var packageId = await CreatePackageAsync();
        var adminClient = _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin");

        var res = await adminClient.PutAsJsonAsync($"/api/v1/subscriptions/packages/{packageId}", new
        {
            Description = "New description", Price = 500_000m, MaxTicketsPerEvent = 100,
            HasAiPoster = true, MaxAiPostersPerMonth = 10, IsActive = true
        });

        res.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    /// <summary>
    /// MLACP-677. Admin ẩn gói khi còn chủ phòng trà đang dùng: chủ vẫn dùng tới hết hạn (đã trả tiền cho kỳ đó), nhưng phải
    /// được BÁO ngay, màn hình của họ biết gói đã ngừng bán (thay cho nút Gia hạn bấm vào mới báo lỗi), và Admin thấy trước
    /// gói đang có bao nhiêu người dùng.
    /// </summary>
    [Fact]
    public async Task AnGoiConNguoiDung_ChuVanDungToiHetHan_DuocBao_VaManHinhBietGoiNgungBan()
    {
        var packageId = await CreatePackageAsync();
        var ownerId = await CreateFreshOwnerAsync();
        var ownerClient = _factory.CreateAuthenticatedClient(ownerId, "Owner");
        var initiation = await (await ownerClient.PostAsJsonAsync("/api/v1/subscriptions/subscribe", new { PackageId = packageId }))
            .Content.ReadFromJsonAsync<SubscriptionInitiationResponse>();
        await ownerClient.GetAsync(
            $"/api/v1/subscriptions/vnpay-return?vnp_TxnRef={initiation!.Data.OrderId}" +
            $"&vnp_ResponseCode=00&vnp_Amount={(long)(initiation.Data.Amount * 100)}");

        var adminClient = _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin");
        var dsAdmin = await adminClient.GetFromJsonAsync<PackageListResponse>("/api/v1/subscriptions/packages?activeOnly=false");
        dsAdmin!.Data.Single(p => p.Id == packageId).ActiveSubscriberCount.Should().Be(1, "Admin thấy trước ẩn gói thì ảnh hưởng ai");
        (await ownerClient.GetFromJsonAsync<PackageListResponse>("/api/v1/subscriptions/packages"))!
            .Data.Should().OnlyContain(p => p.ActiveSubscriberCount == null, "con số kinh doanh này chỉ dành cho Admin");

        (await adminClient.PutAsJsonAsync($"/api/v1/subscriptions/packages/{packageId}", new
        {
            Description = "Test package", Price = 500_000m, MaxTicketsPerEvent = 100, HasAiPoster = true,
            MaxAiPostersPerMonth = 10, IsActive = false
        })).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var cuaToi = await ownerClient.GetFromJsonAsync<MySubResponse>("/api/v1/subscriptions/my");
        cuaToi!.Data.Status.Should().Be("Active", "đã trả tiền cho kỳ này — ẩn gói không cắt ngang");
        cuaToi.Data.PackageOnSale.Should().BeFalse();

        using var scope = _factory.Services.CreateScope();
        var bao = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Notifications.AsNoTracking()
            .SingleAsync(n => n.UserId == ownerId && n.Type == NotificationType.SubscriptionUpdated);
        bao.Title.Should().EndWith("đã ngừng mở bán");
        bao.Body.Should().Contain("tới hết ngày").And.Contain("chọn một gói khác");

        (await ownerClient.PostAsJsonAsync("/api/v1/subscriptions/renew", new { }))
            .StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, "gói ngừng bán không gia hạn được (đã chặn từ trước)");
    }

    private sealed record PackageItem(Guid Id, int? ActiveSubscriberCount);
    private sealed record PackageListResponse(List<PackageItem> Data);
    private sealed record MySub(string Status, bool PackageOnSale);
    private sealed record MySubResponse(MySub Data);

    /// <summary>
    /// Regression test for the OwnerSubscription double-active race (B6/S4, fixed 2026-08-09):
    /// ProcessSubscriptionPaymentCommandHandler used to lock by txnRef, which does nothing when an
    /// owner double-submits Subscribe and ends up with two DIFFERENT Payment rows (two different
    /// txnRefs) — both confirmation callbacks could sail through in parallel and both attempt to
    /// insert an Active OwnerSubscription. The handler now locks by OwnerId instead, and explicitly
    /// checks for an already-Active subscription before inserting a second one. This test fires both
    /// confirmations genuinely concurrently (Task.WhenAll, not sequential awaits) to actually open
    /// the race window rather than just asserting the DB backstop exists.
    /// </summary>
    [Fact]
    public async Task Subscribe_TwoConcurrentPaymentConfirmationsForSameOwner_OnlyOneSubscriptionActivates()
    {
        var packageId = await CreatePackageAsync(price: 250_000m);
        var ownerId = await CreateFreshOwnerAsync();
        var ownerClient = _factory.CreateAuthenticatedClient(ownerId, "Owner");

        var initiation1 = (await (await ownerClient.PostAsJsonAsync(
            "/api/v1/subscriptions/subscribe", new { PackageId = packageId }))
            .Content.ReadFromJsonAsync<SubscriptionInitiationResponse>())!;
        var initiation2 = (await (await ownerClient.PostAsJsonAsync(
            "/api/v1/subscriptions/subscribe", new { PackageId = packageId }))
            .Content.ReadFromJsonAsync<SubscriptionInitiationResponse>())!;

        var noRedirectClient = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var callback1 = noRedirectClient.GetAsync(
            $"/api/v1/subscriptions/vnpay-return?vnp_TxnRef={initiation1.Data.OrderId}" +
            $"&vnp_ResponseCode=00&vnp_Amount={(long)(initiation1.Data.Amount * 100)}");
        var callback2 = noRedirectClient.GetAsync(
            $"/api/v1/subscriptions/vnpay-return?vnp_TxnRef={initiation2.Data.OrderId}" +
            $"&vnp_ResponseCode=00&vnp_Amount={(long)(initiation2.Data.Amount * 100)}");

        await Task.WhenAll(callback1, callback2);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var activeCount = await db.OwnerSubscriptions.CountAsync(
            s => s.OwnerId == ownerId && s.Status == SubscriptionStatus.Active);
        activeCount.Should().Be(1,
            "two concurrently-confirmed payments for the same owner must not both activate a subscription");

        // The second (duplicate) payment must be booked Confirmed, not stranded at Pending — a real
        // charge with no record of ever being resolved is worse than a duplicate subscription would
        // have been, because nothing would ever surface it for a refund.
        var confirmedCount = await db.Payments.CountAsync(
            p => (p.OrderId == initiation1.Data.OrderId || p.OrderId == initiation2.Data.OrderId)
                && p.Status == PaymentStatus.Confirmed);
        confirmedCount.Should().Be(2, "both payments genuinely succeeded at VNPay and must both be reconciled, not left Pending");
    }

    // ─── Renew (honest replacement for the dead AutoRenew field — no silent VNPay recharge is
    // possible, so this is a convenience "skip re-picking the package" endpoint, not true auto-pay) ──

    [Fact]
    public async Task Renew_NoExistingSubscriptionEver_Returns422()
    {
        var ownerId = await CreateFreshOwnerAsync();
        var ownerClient = _factory.CreateAuthenticatedClient(ownerId, "Owner");

        var res = await ownerClient.PostAsync("/api/v1/subscriptions/renew", null);

        res.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Renew_WhileCurrentSubscriptionStillActive_IsAllowedAsEarlyRenewal()
    {
        var packageId = await CreatePackageAsync(price: 250_000m);
        var ownerId = await CreateFreshOwnerAsync();
        var ownerClient = _factory.CreateAuthenticatedClient(ownerId, "Owner");
        var subscribeRes = await ownerClient.PostAsJsonAsync(
            "/api/v1/subscriptions/subscribe", new { PackageId = packageId });
        var initiation = await subscribeRes.Content.ReadFromJsonAsync<SubscriptionInitiationResponse>();
        await ownerClient.GetAsync(
            $"/api/v1/subscriptions/vnpay-return?vnp_TxnRef={initiation!.Data.OrderId}" +
            $"&vnp_ResponseCode=00&vnp_Amount={(long)(initiation.Data.Amount * 100)}");

        var res = await ownerClient.PostAsync("/api/v1/subscriptions/renew", null);

        // MLACP-371: gia han som duoc phep — thoi gian cong noi vao han hien tai (xem SubscriptionChangeTests).
        res.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Renew_AfterPreviousSubscriptionExpired_InitiatesPaymentForSamePackage()
    {
        var packageId = await CreatePackageAsync(price: 250_000m);
        var ownerId = await CreateFreshOwnerAsync();
        var ownerClient = _factory.CreateAuthenticatedClient(ownerId, "Owner");
        var subscribeRes = await ownerClient.PostAsJsonAsync(
            "/api/v1/subscriptions/subscribe", new { PackageId = packageId });
        var initiation = await subscribeRes.Content.ReadFromJsonAsync<SubscriptionInitiationResponse>();
        await ownerClient.GetAsync(
            $"/api/v1/subscriptions/vnpay-return?vnp_TxnRef={initiation!.Data.OrderId}" +
            $"&vnp_ResponseCode=00&vnp_Amount={(long)(initiation.Data.Amount * 100)}");

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var sub = await db.OwnerSubscriptions.SingleAsync(s => s.OwnerId == ownerId);
            sub.ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1);
            await db.SaveChangesAsync();
        }

        var res = await ownerClient.PostAsync("/api/v1/subscriptions/renew", null);

        res.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await res.Content.ReadFromJsonAsync<SubscriptionInitiationResponse>();
        body!.Data.Amount.Should().Be(250_000m, "renew must re-use the same package the owner was last on");
    }

    [Fact]
    public async Task Renew_PackageNoLongerActive_Returns422WithClearMessage()
    {
        var packageId = await CreatePackageAsync(price: 250_000m);
        var ownerId = await CreateFreshOwnerAsync();
        var ownerClient = _factory.CreateAuthenticatedClient(ownerId, "Owner");
        var subscribeRes = await ownerClient.PostAsJsonAsync(
            "/api/v1/subscriptions/subscribe", new { PackageId = packageId });
        var initiation = await subscribeRes.Content.ReadFromJsonAsync<SubscriptionInitiationResponse>();
        await ownerClient.GetAsync(
            $"/api/v1/subscriptions/vnpay-return?vnp_TxnRef={initiation!.Data.OrderId}" +
            $"&vnp_ResponseCode=00&vnp_Amount={(long)(initiation.Data.Amount * 100)}");

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var sub = await db.OwnerSubscriptions.SingleAsync(s => s.OwnerId == ownerId);
            sub.ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1);
            var package = await db.SubscriptionPackages.SingleAsync(p => p.Id == packageId);
            package.IsActive = false;
            await db.SaveChangesAsync();
        }

        var res = await ownerClient.PostAsync("/api/v1/subscriptions/renew", null);

        res.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var body = await res.Content.ReadAsStringAsync();
        body.Should().Contain("không còn mở đăng ký");
    }

    // ─── Cancel (MLACP-371: không gia hạn nữa, gói vẫn dùng tới hết kỳ, không hoàn tiền) ──────────

    [Fact]
    public async Task Cancel_NoSubscriptionEver_Returns422()
    {
        var ownerId = await CreateFreshOwnerAsync();
        var ownerClient = _factory.CreateAuthenticatedClient(ownerId, "Owner");

        var res = await ownerClient.PostAsync("/api/v1/subscriptions/cancel", null);

        res.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Cancel_ActiveSubscription_Returns204_AndKeepsThePlanUntilItsEnd()
    {
        var packageId = await CreatePackageAsync(price: 250_000m);
        var ownerId = await CreateFreshOwnerAsync();
        var ownerClient = _factory.CreateAuthenticatedClient(ownerId, "Owner");
        var subscribeRes = await ownerClient.PostAsJsonAsync(
            "/api/v1/subscriptions/subscribe", new { PackageId = packageId });
        var initiation = await subscribeRes.Content.ReadFromJsonAsync<SubscriptionInitiationResponse>();
        await ownerClient.GetAsync(
            $"/api/v1/subscriptions/vnpay-return?vnp_TxnRef={initiation!.Data.OrderId}" +
            $"&vnp_ResponseCode=00&vnp_Amount={(long)(initiation.Data.Amount * 100)}");

        var res = await ownerClient.PostAsync("/api/v1/subscriptions/cancel", null);

        res.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var sub = await db.OwnerSubscriptions.SingleAsync(s => s.OwnerId == ownerId);
        sub.Status.Should().Be(SubscriptionStatus.Active, "MLACP-371: the plan runs to the end of what was paid for");
        sub.CancelledAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Cancel_ThenSubscribeToAnotherPackage_IsRefused_ChangePackageInstead()
    {
        var packageId = await CreatePackageAsync(price: 250_000m);
        var otherPackageId = await CreatePackageAsync(price: 400_000m);
        var ownerId = await CreateFreshOwnerAsync();
        var ownerClient = _factory.CreateAuthenticatedClient(ownerId, "Owner");
        var subscribeRes = await ownerClient.PostAsJsonAsync(
            "/api/v1/subscriptions/subscribe", new { PackageId = packageId });
        var initiation = await subscribeRes.Content.ReadFromJsonAsync<SubscriptionInitiationResponse>();
        await ownerClient.GetAsync(
            $"/api/v1/subscriptions/vnpay-return?vnp_TxnRef={initiation!.Data.OrderId}" +
            $"&vnp_ResponseCode=00&vnp_Amount={(long)(initiation.Data.Amount * 100)}");
        await ownerClient.PostAsync("/api/v1/subscriptions/cancel", null);

        // MLACP-371: truoc day huy co hieu luc ngay de mo khoa dang ky goi khac — tuc doi goi la mat trang so ngay
        // da tra. Nay huy giu goi toi het ky; doi goi la lenh rieng co quy doi phan con lai.
        var res = await ownerClient.PostAsJsonAsync(
            "/api/v1/subscriptions/subscribe", new { PackageId = otherPackageId });

        res.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await res.Content.ReadAsStringAsync()).Should().Contain("Đổi gói");
    }

    [Fact]
    public async Task Cancel_AlreadyCancelled_Returns409()
    {
        var packageId = await CreatePackageAsync(price: 250_000m);
        var ownerId = await CreateFreshOwnerAsync();
        var ownerClient = _factory.CreateAuthenticatedClient(ownerId, "Owner");
        var subscribeRes = await ownerClient.PostAsJsonAsync(
            "/api/v1/subscriptions/subscribe", new { PackageId = packageId });
        var initiation = await subscribeRes.Content.ReadFromJsonAsync<SubscriptionInitiationResponse>();
        await ownerClient.GetAsync(
            $"/api/v1/subscriptions/vnpay-return?vnp_TxnRef={initiation!.Data.OrderId}" +
            $"&vnp_ResponseCode=00&vnp_Amount={(long)(initiation.Data.Amount * 100)}");
        await ownerClient.PostAsync("/api/v1/subscriptions/cancel", null);

        var res = await ownerClient.PostAsync("/api/v1/subscriptions/cancel", null);

        res.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // MLACP-378: xoa SubscriptionStatus.Suspended (khong noi nao trong src/ gan gia tri nay —
    // CancelSubscriptionCommandHandler chi kiem Status == Active, khong co nhanh rieng cho Suspended,
    // nen dat ten cu "CannotEscapePenaltyByCancelling" nham lan ve mot co che khong ton tai). Bai nay
    // gio dung dung ban chat: gói đã Expired (một trạng thái không phải Active có thật trong hệ thống)
    // thì không huỷ được nữa.
    [Fact]
    public async Task Cancel_AnExpiredPlan_Returns409()
    {
        var packageId = await CreatePackageAsync(price: 250_000m);
        var ownerId = await CreateFreshOwnerAsync();
        var ownerClient = _factory.CreateAuthenticatedClient(ownerId, "Owner");
        var subscribeRes = await ownerClient.PostAsJsonAsync(
            "/api/v1/subscriptions/subscribe", new { PackageId = packageId });
        var initiation = await subscribeRes.Content.ReadFromJsonAsync<SubscriptionInitiationResponse>();
        await ownerClient.GetAsync(
            $"/api/v1/subscriptions/vnpay-return?vnp_TxnRef={initiation!.Data.OrderId}" +
            $"&vnp_ResponseCode=00&vnp_Amount={(long)(initiation.Data.Amount * 100)}");

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var sub = await db.OwnerSubscriptions.SingleAsync(s => s.OwnerId == ownerId);
            sub.Status = SubscriptionStatus.Expired;
            await db.SaveChangesAsync();
        }

        var res = await ownerClient.PostAsync("/api/v1/subscriptions/cancel", null);

        res.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    private sealed record IdResponse(bool Success, Guid Data);
    private sealed record SubscriptionInitiationData(Guid PaymentId, string OrderId, decimal Amount, string PaymentUrl);
    private sealed record SubscriptionInitiationResponse(bool Success, SubscriptionInitiationData Data);
}
