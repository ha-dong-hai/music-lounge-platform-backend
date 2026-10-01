using System.Net;
using FluentAssertions;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.ValueObjects;
using MusicLounge.Tests.Integration.Helpers;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Tests.Integration.CF1;

/// <summary>
/// MLACP-506 (M-405 mục 1). Không gỡ được ảnh đại diện phòng trà: xoá ảnh gallery đang làm đại diện thì ảnh đại diện
/// vẫn trỏ vào ảnh đã xoá, và PUT /image bắt buộc URL không rỗng — khi dọn dữ liệu Azure 29/09 phải thay bằng ảnh giữ chỗ.
/// </summary>
[Collection("Integration")]
public sealed class LoungePrimaryImageRemovalTests
{
    private readonly ApiFactory _factory;

    public LoungePrimaryImageRemovalTests(ApiFactory factory) => _factory = factory;

    private static string Ma() => Guid.NewGuid().ToString("N")[..10];

    private sealed record PhongTra(Guid LoungeId, Guid OwnerId, List<(Guid Id, string Url)> Anh);

    // Phòng trà riêng (mỗi chủ một phòng trà) với các ảnh gallery theo thứ tự; ảnh đại diện = ảnh có chỉ số daiDien.
    private async Task<PhongTra> PhongTraCoAnhAsync(int soAnh, int daiDien)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var chu = new User { Email = $"i506-{Ma()}@test.com", FullName = "Chu 506" };
        var lounge = new MusicLoungeEntity
        {
            Owner = chu, Name = $"Phong {Ma()}", Status = LoungeStatus.Approved,
            Address = new VenueAddress { Street = "1 Le Loi", District = "1", City = "HCM" }
        };
        db.Add(lounge);
        await db.SaveChangesAsync();
        var anh = Enumerable.Range(0, soAnh).Select(i => new LoungeGalleryImage
        {
            LoungeId = lounge.Id, ImageUrl = $"https://storage.example/{Ma()}-{i}.jpg", OrderIndex = i
        }).ToList();
        db.AddRange(anh);
        lounge.PrimaryImageUrl = anh[daiDien].ImageUrl;
        await db.SaveChangesAsync();
        return new PhongTra(lounge.Id, chu.Id, anh.Select(a => (a.Id, a.ImageUrl)).ToList());
    }

    private async Task<string?> AnhDaiDienAsync(Guid loungeId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return (await db.Lounges.FindAsync(loungeId))!.PrimaryImageUrl;
    }

    [Fact]
    public async Task XoaAnhGalleryDangLamDaiDien_DaiDienChuyenSangAnhKeTiep()
    {
        var p = await PhongTraCoAnhAsync(3, daiDien: 0);
        var chu = _factory.CreateAuthenticatedClient(p.OwnerId, "Owner");

        (await chu.DeleteAsync($"/api/v1/lounges/{p.LoungeId}/gallery/{p.Anh[0].Id}")).StatusCode
            .Should().Be(HttpStatusCode.NoContent);

        (await AnhDaiDienAsync(p.LoungeId)).Should().Be(p.Anh[1].Url, "ảnh kế tiếp theo thứ tự hiển thị");
    }

    [Fact]
    public async Task XoaAnhGalleryCuoiCung_DaiDienDeTrong()
    {
        var p = await PhongTraCoAnhAsync(1, daiDien: 0);
        var chu = _factory.CreateAuthenticatedClient(p.OwnerId, "Owner");

        await chu.DeleteAsync($"/api/v1/lounges/{p.LoungeId}/gallery/{p.Anh[0].Id}");

        (await AnhDaiDienAsync(p.LoungeId)).Should().BeNull("không còn ảnh nào thì không trỏ vào ảnh đã xoá");
    }

    [Fact]
    public async Task XoaAnhGalleryKhongPhaiDaiDien_DaiDienGiuNguyen()
    {
        var p = await PhongTraCoAnhAsync(3, daiDien: 2);
        var chu = _factory.CreateAuthenticatedClient(p.OwnerId, "Owner");

        await chu.DeleteAsync($"/api/v1/lounges/{p.LoungeId}/gallery/{p.Anh[0].Id}");

        (await AnhDaiDienAsync(p.LoungeId)).Should().Be(p.Anh[2].Url);
    }

    [Fact]
    public async Task GoAnhDaiDien_ChuPhongTraVaAdminDuoc_NguoiKhac403()
    {
        var p = await PhongTraCoAnhAsync(2, daiDien: 0);

        (await _factory.CreateAuthenticatedClient(SeedHelper.OtherOwnerId, "Owner")
                .DeleteAsync($"/api/v1/lounges/{p.LoungeId}/image")).StatusCode
            .Should().Be(HttpStatusCode.Forbidden);
        (await AnhDaiDienAsync(p.LoungeId)).Should().Be(p.Anh[0].Url);

        (await _factory.CreateAuthenticatedClient(p.OwnerId, "Owner")
                .DeleteAsync($"/api/v1/lounges/{p.LoungeId}/image")).StatusCode
            .Should().Be(HttpStatusCode.NoContent);
        (await AnhDaiDienAsync(p.LoungeId)).Should().BeNull();

        // Admin gỡ được (đúng tình huống dọn ảnh vi phạm bản quyền 29/09).
        var p2 = await PhongTraCoAnhAsync(1, daiDien: 0);
        (await _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin")
                .DeleteAsync($"/api/v1/lounges/{p2.LoungeId}/image")).StatusCode
            .Should().Be(HttpStatusCode.NoContent);
        (await AnhDaiDienAsync(p2.LoungeId)).Should().BeNull();
    }
}
