using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Infrastructure.Persistence;
using MusicLounge.Tests.Integration.Helpers;
using CustomCriteriaEntity = MusicLounge.Domain.Entities.CustomCriteria;

namespace MusicLounge.Tests.Integration.CF1;

/// <summary>
/// Tiêu chí riêng phải sửa tên và tắt được.
///
/// <para>Trước task này tiêu chí tạo xong là VĨNH VIỄN: không có lệnh sửa, xoá, hay ngừng dùng nào. Chủ
/// phòng trà gõ sai tên thì cái tên sai ở lại trên màn hình sửa của MỌI buổi diễn, không đường nào chữa
/// trong sản phẩm. Cột <c>IsActive</c> có sẵn nhưng không dòng mã nào từng đặt nó thành false, nên bộ lọc
/// "chỉ lấy tiêu chí đang dùng" không bao giờ loại được gì và cờ trả về cho giao diện là một trạng thái
/// không thể xảy ra.</para>
/// </summary>
[Collection("Integration")]
public sealed class CustomCriteriaEditableTests
{
    private readonly ApiFactory _factory;

    public CustomCriteriaEditableTests(ApiFactory factory) => _factory = factory;

    private HttpClient Owner() => _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner", SeedHelper.LoungeId);

    private async Task<int> TaoTieuChiAsync(string name)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var c = new CustomCriteriaEntity
        {
            LoungeId = SeedHelper.LoungeId,
            Name = name,
            Key = $"k{Guid.NewGuid():N}"[..12],
            DataType = CustomCriteriaDataType.Text,
            IsActive = true
        };
        db.Add(c);
        await db.SaveChangesAsync();
        return c.Id;
    }

    private static JsonElement Data(string body)
    {
        var root = JsonDocument.Parse(body).RootElement;
        return root.TryGetProperty("data", out var d) ? d : root;
    }

    private async Task<JsonElement?> TimTrongDanhSachAsync(int id, bool includeInactive)
    {
        var res = await Owner().GetAsync(
            $"/api/v1/custom-criteria?loungeId={SeedHelper.LoungeId}&includeInactive={includeInactive}");
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        foreach (var c in Data(await res.Content.ReadAsStringAsync()).EnumerateArray())
            if (c.GetProperty("id").GetInt32() == id) return c;
        return null;
    }

    [Fact]
    public async Task SuaTen_ThiDanhSachHienTenMoi()
    {
        var id = await TaoTieuChiAsync("Ngon ngu bieu dien");   // gõ thiếu dấu

        var res = await Owner().PutAsJsonAsync($"/api/v1/custom-criteria/{id}",
            new { Name = "Ngôn ngữ biểu diễn", IsActive = true });
        res.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var dong = await TimTrongDanhSachAsync(id, includeInactive: false);
        dong.Should().NotBeNull();
        dong!.Value.GetProperty("name").GetString().Should().Be("Ngôn ngữ biểu diễn");
    }

    [Fact]
    public async Task Tat_ThiBienKhoiDanhSachMacDinh_NhungVanTraKhiXinCaDaTat()
    {
        var id = await TaoTieuChiAsync($"Tieu chi tat {Guid.NewGuid():N}"[..20]);

        var res = await Owner().PutAsJsonAsync($"/api/v1/custom-criteria/{id}",
            new { Name = "Tieu chi da tat", IsActive = false });
        res.StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await TimTrongDanhSachAsync(id, includeInactive: false))
            .Should().BeNull("tắt rồi thì không đề xuất khi dựng buổi diễn mới nữa");

        var daTat = await TimTrongDanhSachAsync(id, includeInactive: true);
        daTat.Should().NotBeNull(
            "không xem lại được tiêu chí đã tắt thì nút tắt là cửa một chiều, bật lại cũng không xong");
        daTat!.Value.GetProperty("isActive").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task TatRoiBatLai_ThiQuayVeDanhSachMacDinh()
    {
        var id = await TaoTieuChiAsync($"Tieu chi bat lai {Guid.NewGuid():N}"[..22]);
        var owner = Owner();

        await owner.PutAsJsonAsync($"/api/v1/custom-criteria/{id}", new { Name = "Tam ngung", IsActive = false });

        // Mốc kiểm giữa chừng: không có dòng này thì cả phép kiểm xanh cả khi lệnh sửa KHÔNG LÀM GÌ —
        // tiêu chí chưa từng bị tắt thì đương nhiên vẫn nằm trong danh sách mặc định.
        (await TimTrongDanhSachAsync(id, includeInactive: false))
            .Should().BeNull("phải tắt được thật thì phép kiểm bật lại mới có nghĩa");

        await owner.PutAsJsonAsync($"/api/v1/custom-criteria/{id}", new { Name = "Dung lai", IsActive = true });

        (await TimTrongDanhSachAsync(id, includeInactive: false)).Should().NotBeNull();
    }

    [Fact]
    public async Task Tat_KHONG_XoaGiaTriDaGanChoBuoiDien()
    {
        // Điểm quan trọng nhất: tắt là NGỪNG DÙNG, không phải xoá. Nếu tắt mà mất giá trị cũ thì lịch sử
        // các buổi diễn đã qua bị sửa lại, và đó là lý do không làm lệnh XOÁ hẳn.
        var id = await TaoTieuChiAsync($"Tieu chi giu gia tri {Guid.NewGuid():N}"[..24]);
        var owner = Owner();

        var gan = await owner.PostAsJsonAsync(
            $"/api/v1/custom-criteria/shows/{SeedHelper.ShowId}/values",
            new[] { new { CriteriaId = id, Value = "Acoustic" } });
        gan.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await owner.PutAsJsonAsync($"/api/v1/custom-criteria/{id}", new { Name = "Da ngung dung", IsActive = false });

        var res = await owner.GetAsync($"/api/v1/custom-criteria/shows/{SeedHelper.ShowId}/values");
        var dong = Data(await res.Content.ReadAsStringAsync()).EnumerateArray()
            .FirstOrDefault(v => v.GetProperty("criteriaId").GetInt32() == id);

        dong.ValueKind.Should().NotBe(JsonValueKind.Undefined, "giá trị đã gắn phải còn nguyên sau khi tắt");
        dong.GetProperty("value").GetString().Should().Be("Acoustic");
        dong.GetProperty("criteriaIsActive").GetBoolean().Should().BeFalse(
            "cờ này trả về cho giao diện từ MLACP-469 nhưng tới giờ chưa bao giờ false được");
    }

    [Fact]
    public async Task ChuPhongTraKhac_KhongSuaDuoc()
    {
        var id = await TaoTieuChiAsync($"Tieu chi rieng {Guid.NewGuid():N}"[..20]);
        var other = _factory.CreateAuthenticatedClient(SeedHelper.OtherOwnerId, "Owner", SeedHelper.OtherLoungeId);

        var res = await other.PutAsJsonAsync($"/api/v1/custom-criteria/{id}",
            new { Name = "Doi ten trom", IsActive = true });

        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task TenRong_ThiTuChoi()
    {
        var id = await TaoTieuChiAsync($"Tieu chi ten rong {Guid.NewGuid():N}"[..22]);

        var res = await Owner().PutAsJsonAsync($"/api/v1/custom-criteria/{id}",
            new { Name = "", IsActive = true });

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
