using MusicLounge.Domain.Common;
using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.ValueObjects;
using MusicLounge.Tests.Integration.Helpers;
using MusicLoungeVenue = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Tests.Integration.CF1;

/// <summary>
/// 360° panorama venue tour (Louvre/HCMC-Museum style — distinct from MusicLounge.Model3DUrl's
/// single .glb file). Scenes are gated by the Owner's active subscription (MaxTourScenesSnapshot,
/// D12 snapshot pattern). Hotspots link scenes together (Navigate) or show static text (Info).
///
/// Every test builds its OWN fresh owner+lounge+subscription rather than reusing
/// SeedHelper.LoungeId/OwnerId — this suite's own quota-exhaustion test would otherwise
/// permanently use up the shared seed lounge's scene quota for the rest of the (shared-DB) test run.
/// GET /api/v1/lounges/{id}/tour | POST/DELETE .../tour/scenes | POST/DELETE .../tour/hotspots
/// </summary>
[Collection("Integration")]
public sealed class VenueTourTests
{
    private readonly ApiFactory _factory;
    private static int _freshIdCounter = 9300;

    public VenueTourTests(ApiFactory factory) => _factory = factory;

    /// <summary>Fresh Owner + Lounge + an Active subscription granting maxTourScenes.</summary>
    private async Task<(Guid OwnerId, Guid LoungeId)> CreateOwnerWithLoungeAsync(int maxTourScenes = 5)
    {
        var id = OrderedGuid.New();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        db.Users.Add(new User { Id = id, Email = $"touro{id}@test.com", FullName = "Tour Owner" });
        db.Lounges.Add(new MusicLoungeVenue
        {
            Id = id, OwnerId = id, Name = $"Tour Lounge {id}",
            Address = new VenueAddress { Street = "1 Test", District = "1", City = "HCM" }
        });
        db.SubscriptionPackages.Add(new SubscriptionPackage
        {
            Id = id, Name = $"TourPkg-{id}", Price = 500_000m,
            BillingCycle = SubscriptionBillingCycle.Monthly,
            MaxTicketsPerEvent = 1000, MaxTourScenes = maxTourScenes, IsActive = true
        });
        db.OwnerSubscriptions.Add(new OwnerSubscription
        {
            Id = id, OwnerId = id, PackageId = id,
            StartedAt = DateTimeOffset.UtcNow.AddDays(-1), ExpiresAt = DateTimeOffset.UtcNow.AddDays(29),
            Status = SubscriptionStatus.Active,
            MaxTicketsPerEventSnapshot = 1000, MaxTourScenesSnapshot = maxTourScenes
        });
        await db.SaveChangesAsync();
        return (id, id);
    }

    // AddVenueTourSceneCommandHandler now reads the image back off disk (IImageModerationGate) —
    // needs a real uploaded file, a fake "https://cdn.example.com/..." URL 404s at that read.
    // MLACP-433: mac dinh la anh 360 dung ti le 2:1 — handler nay tu choi anh hep hon.
    private async Task<string> UploadRealImageAsync(HttpClient client, byte[]? anh = null, string duoi = "png")
    {
        using var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(anh ?? AnhMau.Png(4096, 2048));
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(
            duoi == "png" ? "image/png" : "image/jpeg");
        form.Add(fileContent, "file", $"pano-{Guid.NewGuid():N}.{duoi}");

        var res = await client.PostAsync("/api/v1/uploads/images", form);
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<UploadResponse>();
        return body!.Data.Url;
    }

    private async Task<Guid> AddSceneAsync(HttpClient ownerClient, Guid loungeId, string? name = null)
    {
        var imageUrl = await UploadRealImageAsync(ownerClient);
        var res = await ownerClient.PostAsJsonAsync($"/api/v1/lounges/{loungeId}/tour/scenes", new
        {
            ImageUrl = imageUrl,
            Name = name
        });
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<IdResponse>();
        return body!.Data;
    }

    [Fact]
    public async Task AddTourScene_ByOwner_Returns201()
    {
        var (ownerId, loungeId) = await CreateOwnerWithLoungeAsync();
        var client = _factory.CreateAuthenticatedClient(ownerId, "Owner");
        var imageUrl = await UploadRealImageAsync(client);

        var res = await client.PostAsJsonAsync($"/api/v1/lounges/{loungeId}/tour/scenes", new
        {
            ImageUrl = imageUrl,
            Name = "Sảnh chính"
        });

        res.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    /// <summary>
    /// MLACP-433. Trước đây nhận cả ảnh chụp thường — viewer trải ảnh lên mặt cầu nên hiển thị méo. Ảnh 360 chuẩn
    /// (equirectangular) có tỉ lệ 2:1; ảnh hẹp hơn không thể phủ đủ 360° ngang.
    /// </summary>
    [Theory]
    [InlineData(4032, 3024, "png")] // ảnh chụp thường 4:3
    [InlineData(1920, 1080, "png")] // 16:9
    [InlineData(4000, 2030, "png")] // 1.97:1 — ngoài dung sai 1%
    public async Task AddTourScene_NotA360Image_Returns422_NoSceneCreated(int width, int height, string duoi)
    {
        var (ownerId, loungeId) = await CreateOwnerWithLoungeAsync();
        var client = _factory.CreateAuthenticatedClient(ownerId, "Owner");
        var imageUrl = await UploadRealImageAsync(client, AnhMau.Png(width, height), duoi);

        var res = await client.PostAsJsonAsync($"/api/v1/lounges/{loungeId}/tour/scenes",
            new { ImageUrl = imageUrl, Name = (string?)null });

        res.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await res.Content.ReadAsStringAsync()).Should().Contain($"{width}×{height}").And.Contain("2:1");
        await AssertNoScenesAsync(loungeId);
    }

    [Theory]
    [InlineData(4096, 2048)] // 2:1 chuẩn
    [InlineData(4000, 2020)] // 1.98:1 — trong dung sai 1% (vài pixel bị cắt khi chỉnh sửa)
    [InlineData(8000, 2000)] // dải ghép 4:1 (bị cắt bớt trần/sàn) — rộng hơn 2:1 vẫn hợp lệ
    public async Task AddTourScene_Proper360Proportions_Returns201(int width, int height)
    {
        var (ownerId, loungeId) = await CreateOwnerWithLoungeAsync();
        var client = _factory.CreateAuthenticatedClient(ownerId, "Owner");
        var imageUrl = await UploadRealImageAsync(client, AnhMau.Png(width, height));

        var res = await client.PostAsJsonAsync($"/api/v1/lounges/{loungeId}/tour/scenes",
            new { ImageUrl = imageUrl, Name = (string?)null });

        res.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    /// <summary>
    /// MLACP-439. Ảnh panorama thật 8000×3314 (tỉ lệ 2,41 — qua kiểm tỉ lệ) nhưng GPano cho biết chỉ phủ ~192°.
    /// </summary>
    [Fact]
    public async Task AddTourScene_PartialPanoramaByGPano_Returns422_EvenThoughWiderThan2x1()
    {
        var (ownerId, loungeId) = await CreateOwnerWithLoungeAsync();
        var client = _factory.CreateAuthenticatedClient(ownerId, "Owner");
        var imageUrl = await UploadRealImageAsync(client, AnhMau.JpegVoiXmp(8000, 3314, AnhMau.XmpGPanoThat192Do), "jpg");

        var res = await client.PostAsJsonAsync($"/api/v1/lounges/{loungeId}/tour/scenes",
            new { ImageUrl = imageUrl, Name = (string?)null });

        res.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await res.Content.ReadAsStringAsync()).Should().Contain("192°").And.Contain("đủ một vòng");
        await AssertNoScenesAsync(loungeId);
    }

    [Fact]
    public async Task AddTourScene_FullPanoramaByGPano_Returns201()
    {
        var (ownerId, loungeId) = await CreateOwnerWithLoungeAsync();
        var client = _factory.CreateAuthenticatedClient(ownerId, "Owner");
        var imageUrl = await UploadRealImageAsync(client, AnhMau.JpegVoiXmp(4397, 922, AnhMau.XmpGPanoThat360Do), "jpg");

        var res = await client.PostAsJsonAsync($"/api/v1/lounges/{loungeId}/tour/scenes",
            new { ImageUrl = imageUrl, Name = (string?)null });

        res.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task AddTourScene_JpegStored2x1ButRotatedPortraitByExif_Returns422()
    {
        // Lưu 4096×2048 nhưng thẻ EXIF Orientation = 6 (điện thoại cầm dọc): trình duyệt xoay lại khi hiển thị thành
        // 2048×4096. Chỉ đọc kích thước lưu trữ thì lọt qua.
        var (ownerId, loungeId) = await CreateOwnerWithLoungeAsync();
        var client = _factory.CreateAuthenticatedClient(ownerId, "Owner");
        var imageUrl = await UploadRealImageAsync(client, AnhMau.Jpeg(4096, 2048, exifOrientation: 6), "jpg");

        var res = await client.PostAsJsonAsync($"/api/v1/lounges/{loungeId}/tour/scenes",
            new { ImageUrl = imageUrl, Name = (string?)null });

        res.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await res.Content.ReadAsStringAsync()).Should().Contain("2048×4096");
    }

    [Fact]
    public async Task AddTourScene_ImageSizeUnreadable_Returns422()
    {
        // Qua được khâu upload (đúng chữ ký PNG) nhưng không có phần đầu chứa kích thước.
        var (ownerId, loungeId) = await CreateOwnerWithLoungeAsync();
        var client = _factory.CreateAuthenticatedClient(ownerId, "Owner");
        var imageUrl = await UploadRealImageAsync(client, [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0]);

        var res = await client.PostAsJsonAsync($"/api/v1/lounges/{loungeId}/tour/scenes",
            new { ImageUrl = imageUrl, Name = (string?)null });

        res.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await res.Content.ReadAsStringAsync()).Should().Contain("Không đọc được kích thước ảnh");
        await AssertNoScenesAsync(loungeId);
    }

    private async Task AssertNoScenesAsync(Guid loungeId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.VenueTourScenes.CountAsync(s => s.LoungeId == loungeId)).Should().Be(0);
    }

    [Fact]
    public async Task AddTourScene_ByNonOwnerOfVenue_Returns403()
    {
        var (_, loungeId) = await CreateOwnerWithLoungeAsync();
        var otherOwnerClient = _factory.CreateAuthenticatedClient(SeedHelper.OtherOwnerId, "Owner");

        var res = await otherOwnerClient.PostAsJsonAsync($"/api/v1/lounges/{loungeId}/tour/scenes", new
        {
            ImageUrl = "https://cdn.example.com/hijacked.jpg",
            Name = (string?)null
        });

        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AddTourScene_ExceedsSubscriptionQuota_Returns422()
    {
        var (ownerId, loungeId) = await CreateOwnerWithLoungeAsync(maxTourScenes: 2);
        var client = _factory.CreateAuthenticatedClient(ownerId, "Owner");
        await AddSceneAsync(client, loungeId);
        await AddSceneAsync(client, loungeId);

        var res = await client.PostAsJsonAsync($"/api/v1/lounges/{loungeId}/tour/scenes", new
        {
            ImageUrl = "https://cdn.example.com/pano-overflow.jpg",
            Name = (string?)null
        });

        res.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var body = await res.Content.ReadAsStringAsync();
        body.Should().Contain("giới hạn");
    }

    [Fact]
    public async Task AddTourScene_NoActiveSubscription_Returns422()
    {
        var id = OrderedGuid.New();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Users.Add(new User { Id = id, Email = $"notour{id}@test.com", FullName = "NoTour Owner" });
            db.Lounges.Add(new MusicLoungeVenue
            {
                Id = id, OwnerId = id, Name = "NoTour Lounge",
                Address = new VenueAddress { Street = "1 Test", District = "1", City = "HCM" }
            });
            await db.SaveChangesAsync();
        }

        var client = _factory.CreateAuthenticatedClient(id, "Owner");
        var res = await client.PostAsJsonAsync($"/api/v1/lounges/{id}/tour/scenes", new
        {
            ImageUrl = "https://cdn.example.com/pano.jpg",
            Name = (string?)null
        });

        res.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task GetTour_Anonymous_ReturnsScenesWithHotspots()
    {
        var (ownerId, loungeId) = await CreateOwnerWithLoungeAsync();
        var client = _factory.CreateAuthenticatedClient(ownerId, "Owner");
        var scene1 = await AddSceneAsync(client, loungeId, "Sảnh chính");
        var scene2 = await AddSceneAsync(client, loungeId, "Sân khấu");

        var navRes = await client.PostAsJsonAsync(
            $"/api/v1/lounges/{loungeId}/tour/scenes/{scene1}/hotspots", new
            {
                Type = "Navigate", Yaw = 45.0, Pitch = 0.0, Label = "Đi tới sân khấu",
                TargetSceneId = scene2, InfoText = (string?)null
            });
        navRes.StatusCode.Should().Be(HttpStatusCode.Created);

        await client.PostAsJsonAsync(
            $"/api/v1/lounges/{loungeId}/tour/scenes/{scene1}/hotspots", new
            {
                Type = "Info", Yaw = -90.0, Pitch = 10.0, Label = "Quầy bar",
                TargetSceneId = (Guid?)null, InfoText = "Quầy bar phục vụ 18h-24h"
            });

        var anonClient = _factory.CreateClient();
        var res = await anonClient.GetAsync($"/api/v1/lounges/{loungeId}/tour");

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await res.Content.ReadAsStringAsync();
        body.Should().Contain("Sảnh chính").And.Contain("Sân khấu")
            .And.Contain("\"type\":\"Navigate\"").And.Contain("\"type\":\"Info\"")
            .And.Contain("Quầy bar phục vụ 18h-24h");
    }

    [Fact]
    public async Task AddTourHotspot_NavigateWithoutTargetSceneId_Returns400()
    {
        var (ownerId, loungeId) = await CreateOwnerWithLoungeAsync();
        var client = _factory.CreateAuthenticatedClient(ownerId, "Owner");
        var sceneId = await AddSceneAsync(client, loungeId);

        var res = await client.PostAsJsonAsync(
            $"/api/v1/lounges/{loungeId}/tour/scenes/{sceneId}/hotspots", new
            {
                Type = "Navigate", Yaw = 0.0, Pitch = 0.0, Label = (string?)null,
                TargetSceneId = (Guid?)null, InfoText = (string?)null
            });

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AddTourHotspot_TargetSceneFromDifferentLounge_Returns404()
    {
        var (ownerId, loungeId) = await CreateOwnerWithLoungeAsync();
        var (otherOwnerId, otherLoungeId) = await CreateOwnerWithLoungeAsync();
        var ownerClient = _factory.CreateAuthenticatedClient(ownerId, "Owner");
        var otherOwnerClient = _factory.CreateAuthenticatedClient(otherOwnerId, "Owner");
        var sceneInMyLounge = await AddSceneAsync(ownerClient, loungeId);
        var sceneInOtherLounge = await AddSceneAsync(otherOwnerClient, otherLoungeId);

        var res = await ownerClient.PostAsJsonAsync(
            $"/api/v1/lounges/{loungeId}/tour/scenes/{sceneInMyLounge}/hotspots", new
            {
                Type = "Navigate", Yaw = 0.0, Pitch = 0.0, Label = (string?)null,
                TargetSceneId = sceneInOtherLounge, InfoText = (string?)null
            });

        res.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RemoveTourScene_AlsoRemovesHotspotsThatNavigatedToIt()
    {
        var (ownerId, loungeId) = await CreateOwnerWithLoungeAsync();
        var client = _factory.CreateAuthenticatedClient(ownerId, "Owner");
        var sceneA = await AddSceneAsync(client, loungeId);
        var sceneB = await AddSceneAsync(client, loungeId);
        var hotspotRes = await client.PostAsJsonAsync(
            $"/api/v1/lounges/{loungeId}/tour/scenes/{sceneA}/hotspots", new
            {
                Type = "Navigate", Yaw = 0.0, Pitch = 0.0, Label = (string?)null,
                TargetSceneId = sceneB, InfoText = (string?)null
            });
        var hotspotId = (await hotspotRes.Content.ReadFromJsonAsync<IdResponse>())!.Data;

        // Deleting the TARGET must not throw an FK violation (TargetSceneId is Restrict, not
        // Cascade — two cascade paths into the same table isn't allowed by SQL Server), and must
        // clean up the now-dangling hotspot in scene A rather than leaving it pointing nowhere.
        var deleteRes = await client.DeleteAsync($"/api/v1/lounges/{loungeId}/tour/scenes/{sceneB}");
        deleteRes.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var danglingHotspot = await db.VenueTourHotspots.FindAsync(hotspotId);
        danglingHotspot.Should().BeNull("the hotspot that navigated to the deleted scene must be cleaned up too");
    }

    [Fact]
    public async Task RemoveTourHotspot_ByOwner_Returns204()
    {
        var (ownerId, loungeId) = await CreateOwnerWithLoungeAsync();
        var client = _factory.CreateAuthenticatedClient(ownerId, "Owner");
        var sceneId = await AddSceneAsync(client, loungeId);
        var hotspotRes = await client.PostAsJsonAsync(
            $"/api/v1/lounges/{loungeId}/tour/scenes/{sceneId}/hotspots", new
            {
                Type = "Info", Yaw = 0.0, Pitch = 0.0, Label = (string?)null,
                TargetSceneId = (Guid?)null, InfoText = "Test info"
            });
        var hotspotId = (await hotspotRes.Content.ReadFromJsonAsync<IdResponse>())!.Data;

        var res = await client.DeleteAsync($"/api/v1/lounges/{loungeId}/tour/hotspots/{hotspotId}");

        res.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    // ---------- MLACP-586: đổi tên cảnh ----------

    [Fact]
    public async Task RenameTourScene_ByOwner_ChangesNameAndKeepsHotspotsAndPosition()
    {
        var (ownerId, loungeId) = await CreateOwnerWithLoungeAsync();
        var client = _factory.CreateAuthenticatedClient(ownerId, "Owner");
        var sceneId = await AddSceneAsync(client, loungeId); // thêm KHÔNG tên — đúng tình huống chủ dự án gặp ("Cảnh 1")
        (await client.PostAsJsonAsync($"/api/v1/lounges/{loungeId}/tour/scenes/{sceneId}/hotspots", new
        {
            Type = "Info", Yaw = 10.0, Pitch = 0.0, Label = "Quầy pha chế", InfoText = "Mở từ 19:00", TargetSceneId = (Guid?)null
        })).EnsureSuccessStatusCode();
        (await client.PutAsJsonAsync($"/api/v1/lounges/{loungeId}/tour/scenes/{sceneId}/position", new { X = 40.5, Y = 60.25 }))
            .EnsureSuccessStatusCode();

        var res = await client.PutAsJsonAsync($"/api/v1/lounges/{loungeId}/tour/scenes/{sceneId}/name", new { Name = "  Quầy bar  " });

        res.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var body = await (await _factory.CreateClient().GetAsync($"/api/v1/lounges/{loungeId}/tour")).Content.ReadAsStringAsync();
        body.Should().Contain("\"name\":\"Quầy bar\"", "tên được cắt khoảng trắng hai đầu")
            .And.Contain("Quầy pha chế", "đổi tên không được làm mất điểm bấm")
            .And.Contain("\"positionX\":40.5", "đổi tên không được làm mất vị trí trên mặt bằng");
    }

    [Fact]
    public async Task RenameTourScene_BlankName_ClearsTheName()
    {
        var (ownerId, loungeId) = await CreateOwnerWithLoungeAsync();
        var client = _factory.CreateAuthenticatedClient(ownerId, "Owner");
        var sceneId = await AddSceneAsync(client, loungeId, "Sảnh chính");

        var res = await client.PutAsJsonAsync($"/api/v1/lounges/{loungeId}/tour/scenes/{sceneId}/name", new { Name = "   " });

        res.StatusCode.Should().Be(HttpStatusCode.NoContent);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Set<VenueTourScene>().Single(s => s.Id == sceneId).Name.Should().BeNull();
    }

    [Fact]
    public async Task RenameTourScene_ByAnotherOwner_Returns403_NameUnchanged()
    {
        var (ownerId, loungeId) = await CreateOwnerWithLoungeAsync();
        var (otherOwnerId, _) = await CreateOwnerWithLoungeAsync();
        var sceneId = await AddSceneAsync(_factory.CreateAuthenticatedClient(ownerId, "Owner"), loungeId, "Sảnh chính");

        var res = await _factory.CreateAuthenticatedClient(otherOwnerId, "Owner")
            .PutAsJsonAsync($"/api/v1/lounges/{loungeId}/tour/scenes/{sceneId}/name", new { Name = "Chiếm tên" });

        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Set<VenueTourScene>().Single(s => s.Id == sceneId).Name.Should().Be("Sảnh chính");
    }

    [Fact]
    public async Task RenameTourScene_SceneOfAnotherLounge_Returns404()
    {
        var (ownerId, loungeId) = await CreateOwnerWithLoungeAsync();
        var (otherOwnerId, otherLoungeId) = await CreateOwnerWithLoungeAsync();
        var otherScene = await AddSceneAsync(_factory.CreateAuthenticatedClient(otherOwnerId, "Owner"), otherLoungeId, "Của người khác");

        var res = await _factory.CreateAuthenticatedClient(ownerId, "Owner")
            .PutAsJsonAsync($"/api/v1/lounges/{loungeId}/tour/scenes/{otherScene}/name", new { Name = "Đổi trộm" });

        res.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RenameTourScene_NameOver100Chars_Returns400()
    {
        var (ownerId, loungeId) = await CreateOwnerWithLoungeAsync();
        var client = _factory.CreateAuthenticatedClient(ownerId, "Owner");
        var sceneId = await AddSceneAsync(client, loungeId);

        var res = await client.PutAsJsonAsync($"/api/v1/lounges/{loungeId}/tour/scenes/{sceneId}/name", new { Name = new string('a', 101) });

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SetTourScenePosition_ByOwner_PersistsCoordinatesAndSurfacesFloorPlanImage()

    {
        var (ownerId, loungeId) = await CreateOwnerWithLoungeAsync();
        var client = _factory.CreateAuthenticatedClient(ownerId, "Owner");
        await client.PutAsJsonAsync($"/api/v1/lounges/{loungeId}/area-layout-image",
            new { ImageUrl = "https://cdn.example.com/floorplan.jpg" });
        var sceneId = await AddSceneAsync(client, loungeId);

        var res = await client.PutAsJsonAsync(
            $"/api/v1/lounges/{loungeId}/tour/scenes/{sceneId}/position", new { X = 40.5, Y = 60.25 });
        res.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var anonClient = _factory.CreateClient();
        var tourRes = await anonClient.GetAsync($"/api/v1/lounges/{loungeId}/tour");
        var body = await tourRes.Content.ReadAsStringAsync();
        body.Should().Contain("floorplan.jpg").And.Contain("\"positionX\":40.5").And.Contain("\"positionY\":60.25");
    }

    [Fact]
    public async Task SetTourScenePosition_OnlyXProvided_Returns400()
    {
        var (ownerId, loungeId) = await CreateOwnerWithLoungeAsync();
        var client = _factory.CreateAuthenticatedClient(ownerId, "Owner");
        var sceneId = await AddSceneAsync(client, loungeId);

        var res = await client.PutAsJsonAsync(
            $"/api/v1/lounges/{loungeId}/tour/scenes/{sceneId}/position", new { X = 40.0, Y = (double?)null });

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SetTourScenePosition_BothNull_ClearsExistingPosition()
    {
        var (ownerId, loungeId) = await CreateOwnerWithLoungeAsync();
        var client = _factory.CreateAuthenticatedClient(ownerId, "Owner");
        var sceneId = await AddSceneAsync(client, loungeId);
        await client.PutAsJsonAsync($"/api/v1/lounges/{loungeId}/tour/scenes/{sceneId}/position", new { X = 10.0, Y = 20.0 });

        var res = await client.PutAsJsonAsync(
            $"/api/v1/lounges/{loungeId}/tour/scenes/{sceneId}/position", new { X = (double?)null, Y = (double?)null });

        res.StatusCode.Should().Be(HttpStatusCode.NoContent);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var scene = await db.VenueTourScenes.FindAsync(sceneId);
        scene!.PositionX.Should().BeNull();
        scene.PositionY.Should().BeNull();
    }

    // ---- MLACP-555: điểm bấm "Khu" trên ảnh 360 — khách chạm để chọn khu ghế khi mua vé ----

    private async Task<Guid> AddZoneAsync(Guid loungeId, bool isActive = true)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var zone = new SeatingZone { Id = OrderedGuid.New(), LoungeId = loungeId, Name = "Khu VIP", Capacity = 20, IsActive = isActive };
        db.SeatingZones.Add(zone);
        await db.SaveChangesAsync();
        return zone.Id;
    }

    private static object ZoneHotspot(Guid? zoneId) => new
    {
        Type = "Zone", Yaw = 30.0, Pitch = -10.0, Label = "Khu VIP",
        TargetSceneId = (Guid?)null, InfoText = (string?)null, ZoneId = zoneId
    };

    [Fact]
    public async Task AddTourHotspot_Zone_IsReturnedWithZoneIdOnTour()
    {
        var (ownerId, loungeId) = await CreateOwnerWithLoungeAsync();
        var client = _factory.CreateAuthenticatedClient(ownerId, "Owner");
        var sceneId = await AddSceneAsync(client, loungeId);
        var zoneId = await AddZoneAsync(loungeId);

        var res = await client.PostAsJsonAsync($"/api/v1/lounges/{loungeId}/tour/scenes/{sceneId}/hotspots", ZoneHotspot(zoneId));

        res.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await _factory.CreateClient().GetStringAsync($"/api/v1/lounges/{loungeId}/tour");
        body.Should().Contain("\"type\":\"Zone\"").And.Contain($"\"zoneId\":\"{zoneId}\"");
    }

    [Fact]
    public async Task AddTourHotspot_ZoneWithoutZoneId_Returns400()
    {
        var (ownerId, loungeId) = await CreateOwnerWithLoungeAsync();
        var client = _factory.CreateAuthenticatedClient(ownerId, "Owner");
        var sceneId = await AddSceneAsync(client, loungeId);

        var res = await client.PostAsJsonAsync($"/api/v1/lounges/{loungeId}/tour/scenes/{sceneId}/hotspots", ZoneHotspot(null));

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AddTourHotspot_ZoneFromDifferentLounge_Returns404()
    {
        var (ownerId, loungeId) = await CreateOwnerWithLoungeAsync();
        var (_, otherLoungeId) = await CreateOwnerWithLoungeAsync();
        var client = _factory.CreateAuthenticatedClient(ownerId, "Owner");
        var sceneId = await AddSceneAsync(client, loungeId);
        var otherZoneId = await AddZoneAsync(otherLoungeId);

        var res = await client.PostAsJsonAsync($"/api/v1/lounges/{loungeId}/tour/scenes/{sceneId}/hotspots", ZoneHotspot(otherZoneId));

        res.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AddTourHotspot_InactiveZone_Returns422()
    {
        var (ownerId, loungeId) = await CreateOwnerWithLoungeAsync();
        var client = _factory.CreateAuthenticatedClient(ownerId, "Owner");
        var sceneId = await AddSceneAsync(client, loungeId);
        var zoneId = await AddZoneAsync(loungeId, isActive: false);

        var res = await client.PostAsJsonAsync($"/api/v1/lounges/{loungeId}/tour/scenes/{sceneId}/hotspots", ZoneHotspot(zoneId));

        res.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    /// <summary>Khu bị tạm ngưng SAU khi đã đặt điểm: điểm bị ẩn khỏi tour (khách không chạm được khu không bán),
    /// nhưng vẫn giữ trong bảng để mở lại khu là điểm hiện lại. Điểm Info cùng cảnh không bị ảnh hưởng.</summary>
    [Fact]
    public async Task GetTour_ZoneDeactivatedAfterwards_HidesOnlyThatZoneHotspot()
    {
        var (ownerId, loungeId) = await CreateOwnerWithLoungeAsync();
        var client = _factory.CreateAuthenticatedClient(ownerId, "Owner");
        var sceneId = await AddSceneAsync(client, loungeId);
        var zoneId = await AddZoneAsync(loungeId);
        var zoneRes = await client.PostAsJsonAsync($"/api/v1/lounges/{loungeId}/tour/scenes/{sceneId}/hotspots", ZoneHotspot(zoneId));
        var zoneHotspotId = (await zoneRes.Content.ReadFromJsonAsync<IdResponse>())!.Data;
        await client.PostAsJsonAsync($"/api/v1/lounges/{loungeId}/tour/scenes/{sceneId}/hotspots", new
        {
            Type = "Info", Yaw = 0.0, Pitch = 0.0, Label = "Quầy bar",
            TargetSceneId = (Guid?)null, InfoText = "Mở 18h"
        });

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var zone = await db.SeatingZones.FindAsync(zoneId);
            zone!.IsActive = false;
            await db.SaveChangesAsync();
        }

        var body = await _factory.CreateClient().GetStringAsync($"/api/v1/lounges/{loungeId}/tour");
        body.Should().NotContain("\"type\":\"Zone\"").And.Contain("Mở 18h");
        using var scope2 = _factory.Services.CreateScope();
        var db2 = scope2.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db2.VenueTourHotspots.FindAsync(zoneHotspotId)).Should().NotBeNull("ẩn chứ không xoá — mở lại khu là điểm hiện lại");
    }

    private sealed record IdResponse(bool Success, Guid Data);
    private sealed record UploadResponse(bool Success, UploadedUrl Data);
    private sealed record UploadedUrl(string Url);
}
