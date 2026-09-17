using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Lounges;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.ValueObjects;
using MusicLounge.Infrastructure.Jobs;
using MusicLounge.Tests.Integration.Helpers;
using MusicLoungeVenue = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Tests.Integration.CF1;

/// <summary>
/// MLACP-436. Giới hạn số cảnh tour theo gói phải đúng cả khi có yêu cầu đồng thời và khi job ghép ảnh chạy muộn.
///
/// Không chứng minh bằng cách bắn yêu cầu đồng thời: TestServer chạy tuần tự nên test kiểu đó xanh cả khi thiếu khoá
/// (xem LocksHeldUntilCommitTests, MLACP-396). Thay vào đó <see cref="TransactionCommitProbe"/> kiểm ngay lúc ghi cảnh
/// sắp commit rằng khoá theo phòng trà đang bị giữ — đúng điều kiện để yêu cầu thứ hai không đếm được trạng thái cũ.
/// </summary>
[Collection("Integration")]
public sealed class VenueTourSceneQuotaLockTests
{
    private readonly ApiFactory _factory;
    private static int _freshIdCounter = 9750;

    public VenueTourSceneQuotaLockTests(ApiFactory factory) => _factory = factory;

    private async Task<int> TaoPhongTraAsync(int maxTourScenes, bool goiConHan = true)
    {
        var id = Interlocked.Increment(ref _freshIdCounter);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Users.Add(new User { Id = id, Email = $"khoa-tour-{id}@test.com", FullName = "Chu Phong Tra" });
        db.Lounges.Add(new MusicLoungeVenue
        {
            Id = id, OwnerId = id, Name = $"Phong tra {id}",
            Address = new VenueAddress { Street = "1 Test", District = "1", City = "HCM" }
        });
        db.SubscriptionPackages.Add(new SubscriptionPackage
        {
            Id = id, Name = $"Goi-{id}", Price = 500_000m, BillingCycle = SubscriptionBillingCycle.Monthly,
            MaxTicketsPerEvent = 1000, MaxTourScenes = maxTourScenes, IsActive = true
        });
        db.OwnerSubscriptions.Add(new OwnerSubscription
        {
            Id = id, OwnerId = id, PackageId = id, Status = SubscriptionStatus.Active,
            StartedAt = DateTimeOffset.UtcNow.AddDays(-30),
            ExpiresAt = goiConHan ? DateTimeOffset.UtcNow.AddDays(29) : DateTimeOffset.UtcNow.AddMinutes(-1),
            MaxTicketsPerEventSnapshot = 1000, MaxTourScenesSnapshot = maxTourScenes
        });
        await db.SaveChangesAsync();
        return id;
    }

    private async Task TaoCanhAsync(int loungeId, int orderIndex)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.VenueTourScenes.Add(new VenueTourScene
        {
            LoungeId = loungeId, ImageUrl = $"/uploads/{Guid.NewGuid():N}.jpg", OrderIndex = orderIndex
        });
        await db.SaveChangesAsync();
    }

    private async Task<int> TaoLuotPendingAsync(int loungeId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var attempt = new VenueTourStitchAttempt
        {
            LoungeId = loungeId, Status = VenueTourStitchStatus.Pending, CreatedAt = DateTimeOffset.UtcNow
        };
        db.VenueTourStitchAttempts.Add(attempt);
        await db.SaveChangesAsync();
        return attempt.Id;
    }

    private async Task<string> TaiAnh360Async(HttpClient client)
    {
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(AnhMau.Png(4096, 2048));
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(file, "file", $"pano-{Guid.NewGuid():N}.png");
        var res = await client.PostAsync("/api/v1/uploads/images", form);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<UploadResponse>())!.Data.Url;
    }

    /// <summary>Dịch vụ ghép ảnh giả trả về một ảnh ghép thành công.</summary>
    private sealed class GhepThanhCong : IPanoramaStitchingService
    {
        public bool IsConfiguredFor(IReadOnlyList<string> imageUrls) => true;
        public Task<byte[]> StitchAsync(IReadOnlyList<string> imageUrls, CancellationToken ct = default)
            => Task.FromResult(AnhMau.Jpeg(4096, 2048));
    }

    private async Task ChayJobGhepThanhCongAsync(int attemptId, int loungeId)
    {
        using var scope = _factory.Services.CreateScope();
        var job = ActivatorUtilities.CreateInstance<StitchVenueTourSceneJob>(scope.ServiceProvider, new GhepThanhCong());
        await job.ExecuteAsync(attemptId, loungeId, ["/uploads/a.jpg", "/uploads/b.jpg"], "Quầy bar",
            new JobCancellationToken(false));
    }

    /// <summary>Từ một scope khác, thử lấy khoá trong 250ms: true nghĩa là khoá đang bị giữ.</summary>
    private async Task<bool> KhoaDangBiGiuAsync(string key)
    {
        using var scope = _factory.Services.CreateScope();
        var locks = scope.ServiceProvider.GetRequiredService<IAsyncKeyedLock>();
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(250));
        try
        {
            await using var _ = await locks.AcquireAsync(key, cts.Token);
            return false;
        }
        catch (OperationCanceledException)
        {
            return true;
        }
    }

    private async Task<List<bool>> KhoaTaiMoiLanCommitAsync(string key, Func<Task> act)
    {
        var probe = _factory.Services.GetRequiredService<TransactionCommitProbe>();
        var seen = new List<bool>();
        probe.Arm(async () => seen.Add(await KhoaDangBiGiuAsync(key)));
        try
        {
            await act();
        }
        finally
        {
            probe.Disarm();
        }
        return seen;
    }

    private async Task<(VenueTourStitchAttempt Attempt, List<VenueTourScene> Scenes)> DocAsync(int attemptId, int loungeId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return (await db.VenueTourStitchAttempts.AsNoTracking().FirstAsync(a => a.Id == attemptId),
            await db.VenueTourScenes.AsNoTracking().Where(s => s.LoungeId == loungeId).ToListAsync());
    }

    [Fact]
    public async Task ThemCanhTrucTiep_KhoaTheoPhongTraConGiuLucCommit()
    {
        var loungeId = await TaoPhongTraAsync(maxTourScenes: 5);
        var client = _factory.CreateAuthenticatedClient(loungeId, "Owner");
        var anh = await TaiAnh360Async(client);

        var seen = await KhoaTaiMoiLanCommitAsync(VenueTourRules.LockKey(loungeId), async () =>
            (await client.PostAsJsonAsync($"/api/v1/lounges/{loungeId}/tour/scenes", new { ImageUrl = anh, Name = "Sảnh" }))
                .StatusCode.Should().Be(HttpStatusCode.Created));

        seen.Should().NotBeEmpty();
        seen.Last().Should().BeTrue("nhả khoá trước commit thì yêu cầu thứ hai đếm thiếu cảnh vừa thêm và vượt giới hạn gói");
    }

    [Fact]
    public async Task JobGhepAnh_KhoaTheoPhongTraConGiuLucGhiCanh()
    {
        var loungeId = await TaoPhongTraAsync(maxTourScenes: 5);
        var attemptId = await TaoLuotPendingAsync(loungeId);

        var seen = await KhoaTaiMoiLanCommitAsync(VenueTourRules.LockKey(loungeId),
            () => ChayJobGhepThanhCongAsync(attemptId, loungeId));

        seen.Should().NotBeEmpty();
        seen.Last().Should().BeTrue("job phải dùng chung khoá với thêm cảnh trực tiếp khi ghi cảnh");
        var (attempt, scenes) = await DocAsync(attemptId, loungeId);
        attempt.Status.Should().Be(VenueTourStitchStatus.Succeeded);
        scenes.Should().ContainSingle();
    }

    [Fact]
    public async Task JobGhepAnh_GoiDaDayTrongLucCho_KhongTaoCanhVuotGioiHan()
    {
        // Handler đã kiểm lúc tạo lượt (còn chỗ), nhưng trong lúc job nằm chờ, chủ phòng trà thêm cảnh khác.
        var loungeId = await TaoPhongTraAsync(maxTourScenes: 1);
        var attemptId = await TaoLuotPendingAsync(loungeId);
        await TaoCanhAsync(loungeId, orderIndex: 0);

        await ChayJobGhepThanhCongAsync(attemptId, loungeId);

        var (attempt, scenes) = await DocAsync(attemptId, loungeId);
        attempt.Status.Should().Be(VenueTourStitchStatus.Failed);
        attempt.ErrorMessage.Should().Contain("giới hạn 1 scene");
        attempt.FailedBySystem.Should().BeFalse("giới hạn của gói, không phải sự cố hệ thống");
        scenes.Should().ContainSingle("không được vượt số cảnh của gói");
    }

    [Fact]
    public async Task JobGhepAnh_GoiHetHanTrongLucCho_KhongTaoCanh()
    {
        var loungeId = await TaoPhongTraAsync(maxTourScenes: 5, goiConHan: false);
        var attemptId = await TaoLuotPendingAsync(loungeId);

        await ChayJobGhepThanhCongAsync(attemptId, loungeId);

        var (attempt, scenes) = await DocAsync(attemptId, loungeId);
        attempt.Status.Should().Be(VenueTourStitchStatus.Failed);
        attempt.ErrorMessage.Should().Contain("không hỗ trợ tour");
        scenes.Should().BeEmpty();
    }

    [Fact]
    public async Task ThemCanhSauKhiXoaCanhOGiua_KhongTrungThuTu()
    {
        // Cảnh ở vị trí 1 đã bị xoá: còn 0 và 2. Lấy SỐ cảnh (2) làm vị trí thì trùng với cảnh cuối.
        var loungeId = await TaoPhongTraAsync(maxTourScenes: 5);
        await TaoCanhAsync(loungeId, orderIndex: 0);
        await TaoCanhAsync(loungeId, orderIndex: 2);
        var client = _factory.CreateAuthenticatedClient(loungeId, "Owner");
        var anh = await TaiAnh360Async(client);

        var res = await client.PostAsJsonAsync($"/api/v1/lounges/{loungeId}/tour/scenes", new { ImageUrl = anh, Name = "Mới" });

        res.StatusCode.Should().Be(HttpStatusCode.Created);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var orderIndexes = await db.VenueTourScenes.Where(s => s.LoungeId == loungeId).Select(s => s.OrderIndex).ToListAsync();
        orderIndexes.Should().OnlyHaveUniqueItems().And.Contain(3);
    }

    [Fact]
    public void GoiDangHieuLuc_LayGoiMoiNhatConHan()
    {
        var now = DateTimeOffset.UtcNow;
        var subs = new[]
        {
            new OwnerSubscription { Status = SubscriptionStatus.Active, StartedAt = now.AddDays(-60), ExpiresAt = now.AddDays(-30), MaxTourScenesSnapshot = 20 },
            new OwnerSubscription { Status = SubscriptionStatus.Active, StartedAt = now.AddDays(-10), ExpiresAt = now.AddDays(20), MaxTourScenesSnapshot = 5 },
            new OwnerSubscription { Status = SubscriptionStatus.Cancelled, StartedAt = now.AddDays(-1), ExpiresAt = now.AddDays(29), MaxTourScenesSnapshot = 50 },
        };

        VenueTourRules.MaxScenes(subs, now).Should().Be(5);
        VenueTourRules.MaxScenes([], now).Should().Be(0);
        VenueTourRules.NextOrderIndex([]).Should().Be(0);
    }

    private sealed record UploadResponse(bool Success, UploadedUrl Data);
    private sealed record UploadedUrl(string Url);
}
