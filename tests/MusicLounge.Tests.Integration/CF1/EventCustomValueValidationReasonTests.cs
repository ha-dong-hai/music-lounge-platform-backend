using System.Net;
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
/// Đường đọc phải nói rõ giá trị đang lưu có hợp lệ không, và vì sao không.
///
/// <para>MLACP-470 chặn giá trị sai ở lệnh GHI, nhưng giá trị sai ghi TRƯỚC đó vẫn nằm nguyên trong cơ sở
/// dữ liệu. Ô chọn không khớp lựa chọn nào thì màn hình hiện ô TRỐNG — trông y hệt "chưa đặt" — và vì
/// lệnh ghi là THAY THẾ TOÀN BỘ nên lần Lưu kế tiếp xoá luôn giá trị đó, không ai thấy.</para>
///
/// <para>Không để mỗi giao diện tự chép lại luật so khớp: hiện đã có hai bản (C# và JavaScript) và ứng
/// dụng nhân viên sẽ là bản thứ ba. Hai bản đầu đã từng lệch nhau ở hai ca, chỉ lộ ra khi đối chiếu tay.
/// Máy chủ trả sẵn lý do, tính bằng ĐÚNG hàm mà lệnh ghi dùng để từ chối.</para>
/// </summary>
[Collection("Integration")]
public sealed class EventCustomValueValidationReasonTests
{
    private readonly ApiFactory _factory;

    public EventCustomValueValidationReasonTests(ApiFactory factory) => _factory = factory;

    /// <summary>
    /// Ghi thẳng vào cơ sở dữ liệu, KHÔNG qua API: dựng lại đúng dòng dữ liệu cũ có từ trước khi có hàng
    /// rào. Đi qua API thì lệnh ghi sẽ từ chối, và ca cần kiểm sẽ không bao giờ dựng được.
    /// </summary>
    private async Task<int> TaoTieuChiVaGiaTriAsync(
        CustomCriteriaDataType dataType, string? options, string value)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var c = new CustomCriteriaEntity
        {
            LoungeId = SeedHelper.LoungeId,
            Name = $"TC {Guid.NewGuid():N}"[..16],
            Key = $"k{Guid.NewGuid():N}"[..12],
            DataType = dataType,
            Options = options,
            IsActive = true
        };
        db.Add(c);
        await db.SaveChangesAsync();

        db.Add(new EventCustomValue { ShowId = SeedHelper.ShowId, CriteriaId = c.Id, Value = value });
        await db.SaveChangesAsync();
        return c.Id;
    }

    private async Task<JsonElement> DocAsync(int criteriaId)
    {
        var owner = _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner", SeedHelper.LoungeId);
        var res = await owner.GetAsync($"/api/v1/custom-criteria/shows/{SeedHelper.ShowId}/values");
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        var root = JsonDocument.Parse(await res.Content.ReadAsStringAsync()).RootElement;
        var data = root.TryGetProperty("data", out var d) ? d : root;
        return data.EnumerateArray().First(v => v.GetProperty("criteriaId").GetInt32() == criteriaId);
    }

    [Fact]
    public async Task GiaTriCuLacKhoiDanhSachLuaChon_TraVeLyDo()
    {
        var id = await TaoTieuChiVaGiaTriAsync(
            CustomCriteriaDataType.Select, "[\"Bolero\",\"Acoustic\"]", "Xyz");

        var dong = await DocAsync(id);
        var lyDo = dong.GetProperty("validationError").GetString();

        lyDo.Should().NotBeNull("ô chọn hiện trống trông y hệt chưa đặt; không nói ra thì lần Lưu sau xoá mất");
        lyDo.Should().Contain("Bolero", "phải nêu danh sách hợp lệ để người dùng chọn lại được");
    }

    [Fact]
    public async Task GiaTriHopLe_ThiKhongCoLyDo()
    {
        var id = await TaoTieuChiVaGiaTriAsync(
            CustomCriteriaDataType.Select, "[\"Bolero\",\"Acoustic\"]", "Bolero");

        (await DocAsync(id)).GetProperty("validationError").ValueKind
            .Should().Be(JsonValueKind.Null, "giá trị đúng mà bị gắn nhãn sai thì người dùng sửa cái không hỏng");
    }

    [Fact]
    public async Task GiaTriCuNgoaiKhoang_TraVeLyDo()
    {
        var id = await TaoTieuChiVaGiaTriAsync(
            CustomCriteriaDataType.Range, "{\"min\":1,\"max\":5}", "99");

        (await DocAsync(id)).GetProperty("validationError").GetString()
            .Should().NotBeNull("không riêng ô chọn: số ngoài khoảng cũng phải nói ra");
    }

    [Fact]
    public async Task GiaTriCuKhongPhaiDungSai_TraVeLyDo()
    {
        var id = await TaoTieuChiVaGiaTriAsync(CustomCriteriaDataType.Boolean, null, "co le vay");

        (await DocAsync(id)).GetProperty("validationError").GetString()
            .Should().NotBeNull();
    }

    [Fact]
    public async Task GiaTriCuDangJsonDongGoi_KHONG_BiBaoSai()
    {
        // Dạng hệ thống từng tự sinh ra. Lệnh ghi bóc một lớp nháy rồi mới đối chiếu, nên đường đọc cũng
        // phải coi là hợp lệ — báo sai ở đây là xui người dùng "sửa" thứ không hỏng.
        var id = await TaoTieuChiVaGiaTriAsync(
            CustomCriteriaDataType.Select, "[\"Bolero\"]", "\"Bolero\"");

        (await DocAsync(id)).GetProperty("validationError").ValueKind
            .Should().Be(JsonValueKind.Null);
    }
}
