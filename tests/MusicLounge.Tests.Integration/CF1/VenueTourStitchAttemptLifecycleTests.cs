using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using FluentAssertions;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.Exceptions;
using MusicLounge.Domain.ValueObjects;
using MusicLounge.Infrastructure.Jobs;
using MusicLounge.Tests.Integration.Fakes;
using MusicLounge.Tests.Integration.Helpers;
using MusicLoungeVenue = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Tests.Integration.CF1;

/// <summary>
/// MLACP-435. Vòng đời một lượt ghép ảnh 360:
///   - Job không có chính sách thử lại → Hangfire mặc định thử lại 10 lần: ghép lại từ đầu, và hết lượt thì lượt ghép
///     kẹt Pending mãi mãi.
///   - Deploy/kill giữa chừng cũng để lại lượt Pending → cần job dọn.
///   - Giới hạn 20 lượt trọn đời từng đếm cả lỗi hệ thống → dịch vụ ngừng vài lần là phòng trà bị khoá tính năng.
/// </summary>
[Collection("Integration")]
public sealed class VenueTourStitchAttemptLifecycleTests
{
    private readonly ApiFactory _factory;
    private static int _freshIdCounter = 9700;

    public VenueTourStitchAttemptLifecycleTests(ApiFactory factory) => _factory = factory;

    private async Task<int> TaoPhongTraCoGoiAsync()
    {
        var id = Interlocked.Increment(ref _freshIdCounter);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Users.Add(new User { Id = id, Email = $"vong-doi-ghep-{id}@test.com", FullName = "Chu Phong Tra" });
        db.Lounges.Add(new MusicLoungeVenue
        {
            Id = id, OwnerId = id, Name = $"Phong tra {id}",
            Address = new VenueAddress { Street = "1 Test", District = "1", City = "HCM" }
        });
        db.SubscriptionPackages.Add(new SubscriptionPackage
        {
            Id = id, Name = $"Goi-{id}", Price = 500_000m, BillingCycle = SubscriptionBillingCycle.Monthly,
            MaxTicketsPerEvent = 1000, MaxTourScenes = 50, IsActive = true
        });
        db.OwnerSubscriptions.Add(new OwnerSubscription
        {
            Id = id, OwnerId = id, PackageId = id, Status = SubscriptionStatus.Active,
            StartedAt = DateTimeOffset.UtcNow.AddDays(-1), ExpiresAt = DateTimeOffset.UtcNow.AddDays(29),
            MaxTicketsPerEventSnapshot = 1000, MaxTourScenesSnapshot = 50
        });
        await db.SaveChangesAsync();
        return id;
    }

    private async Task<int> TaoLuotAsync(int loungeId, VenueTourStitchStatus status, bool failedBySystem = false,
        DateTimeOffset? createdAt = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var attempt = new VenueTourStitchAttempt
        {
            LoungeId = loungeId, Status = status, FailedBySystem = failedBySystem,
            CreatedAt = createdAt ?? DateTimeOffset.UtcNow
        };
        db.VenueTourStitchAttempts.Add(attempt);
        await db.SaveChangesAsync();
        return attempt.Id;
    }

    private async Task<VenueTourStitchAttempt> DocLuotAsync(int attemptId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.VenueTourStitchAttempts.AsNoTracking().FirstAsync(a => a.Id == attemptId);
    }

    /// <summary>Dịch vụ ghép ảnh giả ném đúng loại lỗi cần kiểm.</summary>
    private sealed class DichVuNem(Exception loi) : IPanoramaStitchingService
    {
        public bool IsConfiguredFor(IReadOnlyList<string> imageUrls) => true;
        public Task<byte[]> StitchAsync(IReadOnlyList<string> imageUrls, CancellationToken ct = default) => throw loi;
    }

    private async Task ChayJobAsync(int attemptId, int loungeId, IPanoramaStitchingService? stitcher = null)
    {
        using var scope = _factory.Services.CreateScope();
        var job = stitcher is null
            ? scope.ServiceProvider.GetRequiredService<StitchVenueTourSceneJob>()
            : ActivatorUtilities.CreateInstance<StitchVenueTourSceneJob>(scope.ServiceProvider, stitcher);
        await job.ExecuteAsync(attemptId, loungeId, ["/uploads/a.jpg", "/uploads/b.jpg"], null, new JobCancellationToken(false));
    }

    private static object ThanYeuCau() => new
    {
        SourceImageUrls = new[] { $"/uploads/{Guid.NewGuid():N}-1.jpg", $"/uploads/{Guid.NewGuid():N}-2.jpg" },
        Name = (string?)null
    };

    [Fact]
    public async Task LuotThatBaiDoHeThong_KhongTinhVaoGioiHanSoLanGhep()
    {
        var loungeId = await TaoPhongTraCoGoiAsync();
        // Đủ 20 lượt (mặc định tour_stitch_max_attempts_per_lounge) nhưng toàn lỗi phía hệ thống.
        for (var i = 0; i < 20; i++)
            await TaoLuotAsync(loungeId, VenueTourStitchStatus.Failed, failedBySystem: true);

        var client = _factory.CreateAuthenticatedClient(loungeId, "Owner");
        var res = await client.PostAsJsonAsync($"/api/v1/lounges/{loungeId}/tour/scenes/stitch", ThanYeuCau());

        res.StatusCode.Should().Be(HttpStatusCode.Accepted,
            "dịch vụ ngừng vài lần không được làm phòng trà bị khoá tính năng vĩnh viễn");
    }

    [Fact]
    public async Task LuotThatBaiDoBoAnh_VanTinhVaoGioiHan()
    {
        var loungeId = await TaoPhongTraCoGoiAsync();
        for (var i = 0; i < 20; i++)
            await TaoLuotAsync(loungeId, VenueTourStitchStatus.Failed, failedBySystem: false);

        var client = _factory.CreateAuthenticatedClient(loungeId, "Owner");
        var res = await client.PostAsJsonAsync($"/api/v1/lounges/{loungeId}/tour/scenes/stitch", ThanYeuCau());

        res.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, "CPU đã chạy cho các lượt này — giới hạn chống lạm dụng phải giữ");
        (await res.Content.ReadAsStringAsync()).Should().Contain("giới hạn");
    }

    [Fact]
    public async Task Job_LoiHeThong_DanhDauFailedBySystem()
    {
        var loungeId = await TaoPhongTraCoGoiAsync();
        var attemptId = await TaoLuotAsync(loungeId, VenueTourStitchStatus.Pending);

        await ChayJobAsync(attemptId, loungeId); // bản giả mặc định của ApiFactory: ExternalServiceException

        var attempt = await DocLuotAsync(attemptId);
        attempt.Status.Should().Be(VenueTourStitchStatus.Failed);
        attempt.FailedBySystem.Should().BeTrue();
        attempt.ErrorMessage.Should().Be(FakePanoramaStitchingService.ThongBaoLoi);
    }

    [Fact]
    public async Task Job_LoiDoBoAnh_KhongDanhDauLoiHeThong_GiuLoiHuongDan()
    {
        var loungeId = await TaoPhongTraCoGoiAsync();
        var attemptId = await TaoLuotAsync(loungeId, VenueTourStitchStatus.Pending);
        const string lyDo = "Ảnh #2 không tìm đủ điểm trùng khớp với các ảnh còn lại.";

        await ChayJobAsync(attemptId, loungeId, new DichVuNem(new DomainException(lyDo)));

        var attempt = await DocLuotAsync(attemptId);
        attempt.Status.Should().Be(VenueTourStitchStatus.Failed);
        attempt.FailedBySystem.Should().BeFalse();
        attempt.ErrorMessage.Should().Be(lyDo);
    }

    [Fact]
    public async Task Job_LoiKhongLuongTruoc_DongLuotGhep_VaVanNemLoiDeDashboardThay()
    {
        var loungeId = await TaoPhongTraCoGoiAsync();
        var attemptId = await TaoLuotAsync(loungeId, VenueTourStitchStatus.Pending);

        var act = () => ChayJobAsync(attemptId, loungeId, new DichVuNem(new IOException("đĩa đầy")));

        await act.Should().ThrowAsync<IOException>();
        var attempt = await DocLuotAsync(attemptId);
        attempt.Status.Should().Be(VenueTourStitchStatus.Failed, "không được để chủ phòng trà chờ vô hạn");
        attempt.FailedBySystem.Should().BeTrue();
        attempt.ErrorMessage.Should().Be(StitchVenueTourSceneJob.ThongBaoGianDoan);
    }

    [Fact]
    public void Job_KhongTuThuLai()
    {
        // Không khai báo thì Hangfire thử lại 10 lần: mỗi lần ghép lại từ đầu, đốt CPU.
        var retry = typeof(StitchVenueTourSceneJob).GetCustomAttribute<AutomaticRetryAttribute>();
        retry.Should().NotBeNull();
        retry!.Attempts.Should().Be(0);
    }

    [Fact]
    public async Task JobDonLuotKet_DongLuotPendingQuaHan_KhongDungLuotMoiHayLuotDaXong()
    {
        var loungeId = await TaoPhongTraCoGoiAsync();
        var quaHan = await TaoLuotAsync(loungeId, VenueTourStitchStatus.Pending,
            createdAt: DateTimeOffset.UtcNow - ExpireStuckStitchAttemptsJob.QuaHan - TimeSpan.FromMinutes(1));
        var conMoi = await TaoLuotAsync(loungeId, VenueTourStitchStatus.Pending,
            createdAt: DateTimeOffset.UtcNow.AddMinutes(-5));
        var daXong = await TaoLuotAsync(loungeId, VenueTourStitchStatus.Succeeded,
            createdAt: DateTimeOffset.UtcNow.AddHours(-3));

        using (var scope = _factory.Services.CreateScope())
        {
            var job = scope.ServiceProvider.GetRequiredService<ExpireStuckStitchAttemptsJob>();
            await job.ExecuteAsync(new JobCancellationToken(false));
        }

        var a = await DocLuotAsync(quaHan);
        a.Status.Should().Be(VenueTourStitchStatus.Failed);
        a.FailedBySystem.Should().BeTrue("lượt kẹt là lỗi phía hệ thống — không tính vào giới hạn");
        a.ErrorMessage.Should().Be(StitchVenueTourSceneJob.ThongBaoGianDoan);
        (await DocLuotAsync(conMoi)).Status.Should().Be(VenueTourStitchStatus.Pending);
        (await DocLuotAsync(daXong)).Status.Should().Be(VenueTourStitchStatus.Succeeded);
    }

    [Fact]
    public async Task JobGhepChaySauKhiLuotDaBiDong_KhongLamGi()
    {
        var loungeId = await TaoPhongTraCoGoiAsync();
        var attemptId = await TaoLuotAsync(loungeId, VenueTourStitchStatus.Failed, failedBySystem: true);
        var dichVu = new DichVuNem(new InvalidOperationException("không được gọi tới dịch vụ"));

        await ChayJobAsync(attemptId, loungeId, dichVu);

        (await DocLuotAsync(attemptId)).Status.Should().Be(VenueTourStitchStatus.Failed);
    }
}
