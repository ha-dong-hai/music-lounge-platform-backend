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

namespace MusicLounge.Tests.Integration.Moderations;

/// <summary>
/// MLACP-692. Ảnh thư viện / cảnh 360 bị AI gắn cờ "cần xem" tạo bản ghi chờ duyệt nhưng trước đây KHÔNG có đường nào để
/// Admin xử lý — kẹt trong hàng chờ mãi (Azure 06/10: "Chờ duyệt 3" nhưng các tab chỉ cộng được 2). Nay Admin duyệt được:
/// đồng ý thì giữ, từ chối thì gỡ nội dung đúng như chủ phòng trà tự xoá, và báo chủ phòng trà.
/// </summary>
[Collection("Integration")]
public sealed class LoungeMediaReviewTests
{
    private readonly ApiFactory _factory;

    public LoungeMediaReviewTests(ApiFactory factory) => _factory = factory;

    private HttpClient Admin() => _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin");

    private sealed record Seeded(Guid OwnerId, Guid LoungeId, Guid ImageId, Guid SceneA, Guid SceneB, Guid HotspotToB, Guid AttemptId);

    private async Task<Seeded> SeedAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var owner = new User { Email = $"media-{Guid.NewGuid():N}@test.com", FullName = "Chu phong tra" };
        db.Users.Add(owner);
        await db.SaveChangesAsync();
        var anhUrl = $"https://img.test/{Guid.NewGuid():N}.jpg";
        var lounge = new MusicLoungeVenue
        {
            OwnerId = owner.Id, Name = $"Media-{Guid.NewGuid():N}"[..20], Status = LoungeStatus.Approved, PrimaryImageUrl = anhUrl,
            Address = new VenueAddress { Street = "1 Test St", District = "1", City = "HCM" }
        };
        db.Add(lounge);
        await db.SaveChangesAsync();

        var image = new LoungeGalleryImage { LoungeId = lounge.Id, ImageUrl = anhUrl, OrderIndex = 0 };
        var sceneA = new VenueTourScene { LoungeId = lounge.Id, ImageUrl = "https://img.test/a.jpg", Name = "Sảnh", OrderIndex = 0 };
        var sceneB = new VenueTourScene { LoungeId = lounge.Id, ImageUrl = "https://img.test/b.jpg", Name = "Quầy bar", OrderIndex = 1 };
        db.AddRange(image, sceneA, sceneB);
        await db.SaveChangesAsync();

        var hotspot = new VenueTourHotspot { SceneId = sceneA.Id, TargetSceneId = sceneB.Id, Label = "Tới quầy bar" };
        var attempt = new VenueTourStitchAttempt { LoungeId = lounge.Id, Status = VenueTourStitchStatus.Succeeded, ResultSceneId = sceneB.Id, CreatedAt = DateTimeOffset.UtcNow };
        db.AddRange(hotspot, attempt);
        foreach (var (loai, id) in new[] { (ModerationTargetType.GalleryImage, image.Id), (ModerationTargetType.TourScene, sceneB.Id) })
            db.Add(new EventModeration
            {
                TargetType = loai, TargetId = id, AiScore = 0.6f, RiskLevel = ModerationRiskLevel.Medium,
                FlagReason = "Ảnh có thể không phù hợp", SlaDeadline = DateTimeOffset.UtcNow.AddHours(24)
            });
        await db.SaveChangesAsync();
        return new Seeded(owner.Id, lounge.Id, image.Id, sceneA.Id, sceneB.Id, hotspot.Id, attempt.Id);
    }

    private Task<HttpResponseMessage> DuyetAsync(string loai, Guid id, string decision, string? note) =>
        Admin().PostAsJsonAsync($"/api/v1/moderations/{loai}/{id}/review", new { Decision = decision, ReviewNote = note });

    private sealed record Envelope(bool Success, Trang Data);
    private sealed record Trang(List<Dong> Items);
    private sealed record Dong(Guid TargetId, string TargetType, string? TargetImageUrl, string? LoungeName);

    [Fact]
    public async Task HangCho_CoAnhVaTenPhongTra_DeAdminNhinThayThuMinhDuyet()
    {
        var s = await SeedAsync();
        var res = await Admin().GetFromJsonAsync<Envelope>("/api/v1/moderations/pending?targetType=TourScene&pageSize=100");
        var dong = res!.Data.Items.Single(x => x.TargetId == s.SceneB);
        dong.TargetImageUrl.Should().Be("https://img.test/b.jpg");
        dong.LoungeName.Should().StartWith("Media-");
    }

    [Fact]
    public async Task DongY_AnhThuVien_GiuAnh_DongBanGhi()
    {
        var s = await SeedAsync();
        (await DuyetAsync("gallery-images", s.ImageId, "Approved", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.Set<LoungeGalleryImage>().AnyAsync(g => g.Id == s.ImageId)).Should().BeTrue();
        (await db.Set<EventModeration>().SingleAsync(m => m.TargetId == s.ImageId)).AdminDecision.Should().Be(ModerationDecision.Approved);
    }

    [Fact]
    public async Task TuChoi_CanhTour_GoCanh_GoDiemBamTroToi_GoLienKetLuotGhep_BaoChuPhongTra()
    {
        var s = await SeedAsync();
        (await DuyetAsync("tour-scenes", s.SceneB, "Rejected", "Ảnh có người không đồng ý xuất hiện")).StatusCode
            .Should().Be(HttpStatusCode.NoContent);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.Set<VenueTourScene>().AnyAsync(x => x.Id == s.SceneB)).Should().BeFalse("từ chối thì gỡ cảnh khỏi tour");
        (await db.Set<VenueTourScene>().AnyAsync(x => x.Id == s.SceneA)).Should().BeTrue();
        (await db.Set<VenueTourHotspot>().AnyAsync(h => h.Id == s.HotspotToB)).Should().BeFalse("điểm bấm trỏ tới cảnh đã gỡ phải đi theo");
        (await db.Set<VenueTourStitchAttempt>().SingleAsync(a => a.Id == s.AttemptId)).ResultSceneId.Should().BeNull();
        (await db.Set<EventModeration>().SingleAsync(m => m.TargetId == s.SceneB)).AdminDecision.Should().Be(ModerationDecision.Rejected);
        var bao = await db.Set<Notification>().Where(n => n.UserId == s.OwnerId && n.Type == NotificationType.ModerationResult).ToListAsync();
        bao.Should().ContainSingle().Which.Body.Should().Contain("Quầy bar").And.Contain("Ảnh có người không đồng ý xuất hiện");

        (await DuyetAsync("tour-scenes", s.SceneB, "Approved", null)).StatusCode.Should().Be(HttpStatusCode.Conflict, "chỉ duyệt một lần");
    }

    [Fact]
    public async Task TuChoi_AnhDaiDien_ThiAnhDaiDienDuocGoTheo()
    {
        var s = await SeedAsync();
        (await DuyetAsync("gallery-images", s.ImageId, "Rejected", "Ảnh mờ, không thể hiện phòng trà")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.Set<LoungeGalleryImage>().AnyAsync(g => g.Id == s.ImageId)).Should().BeFalse();
        (await db.Lounges.AsNoTracking().SingleAsync(l => l.Id == s.LoungeId)).PrimaryImageUrl.Should().BeNull("MLACP-506: ảnh đại diện không trỏ vào ảnh đã gỡ");
    }

    [Fact]
    public async Task TuChoi_KhongGhiLyDo_BiChan()
        => (await DuyetAsync("gallery-images", (await SeedAsync()).ImageId, "Rejected", "  ")).StatusCode.Should().Be(HttpStatusCode.BadRequest);

    [Fact]
    public async Task NoiDungDaBiChuXoaTruoc_VanDongDuocBanGhi_KhongKetHangCho()
    {
        var s = await SeedAsync();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Remove(await db.Set<LoungeGalleryImage>().SingleAsync(g => g.Id == s.ImageId));
            await db.SaveChangesAsync();
        }
        (await DuyetAsync("gallery-images", s.ImageId, "Rejected", "Ảnh không phù hợp")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var scope2 = _factory.Services.CreateScope();
        (await scope2.ServiceProvider.GetRequiredService<ApplicationDbContext>().Set<EventModeration>().SingleAsync(m => m.TargetId == s.ImageId))
            .AdminDecision.Should().Be(ModerationDecision.Rejected);
    }
}
