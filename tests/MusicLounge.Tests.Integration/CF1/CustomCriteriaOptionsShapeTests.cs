using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using MusicLounge.Tests.Integration.Helpers;

namespace MusicLounge.Tests.Integration.CF1;

/// <summary>
/// Options của tiêu chí phải có HÌNH DẠNG dùng được, không chỉ là JSON hợp lệ.
///
/// <para>MLACP-470 dạy máy chủ đối chiếu giá trị với kiểu dữ liệu, nhưng cố ý không bịa luật khi đọc
/// không ra danh sách lựa chọn. Chỗ hở nằm ở lệnh TẠO: validator lúc đó chỉ đòi "JSON hợp lệ", nên tạo
/// được tiêu chí Select với Options là <c>[1,2,3]</c> — rồi mọi chuỗi gắn vào tiêu chí ấy đều lọt, vì
/// bên đối chiếu đọc không ra danh sách. Không phải dòng dữ liệu cũ: đi thẳng qua API công khai.</para>
///
/// <para>Kèm theo là ca Boolean: <c>bool.TryParse</c> nhận cả "True", nên cột giá trị chứa được ba dạng
/// của cùng một ý. Màn hình nào dựng ô chọn bằng hai lựa chọn chữ thường sẽ không khớp dòng "True" — ô
/// hiện trống như chưa đặt, và lệnh ghi THAY THẾ TOÀN BỘ nên lần Lưu sau xoá mất giá trị, không ai thấy.
/// (Phiên làm giao diện phát hiện khi đối chiếu hai bên, 20/09/2026.)</para>
/// </summary>
[Collection("Integration")]
public sealed class CustomCriteriaOptionsShapeTests
{
    private readonly ApiFactory _factory;

    public CustomCriteriaOptionsShapeTests(ApiFactory factory) => _factory = factory;

    private sealed record DataResponse<T>(T Data);

    private async Task<HttpResponseMessage> TaoAsync(string dataType, string? options)
    {
        var owner = _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner", SeedHelper.LoungeId);
        return await owner.PostAsJsonAsync("/api/v1/custom-criteria", new
        {
            LoungeId = SeedHelper.LoungeId,
            Name = $"Tieu chi {Guid.NewGuid():N}"[..24],
            Key = $"k{Guid.NewGuid():N}"[..12],
            DataType = dataType,
            Options = options
        });
    }

    [Fact]
    public async Task Select_OptionsLaMangSo_ThiTuChoiNgayLucTao()
    {
        // Nếu để lọt, tiêu chí này thành ô chọn mà mọi chuỗi đều hợp lệ — hàng rào kiểu dữ liệu biến mất
        // đúng ở chỗ nó cần nhất.
        var res = await TaoAsync("Select", "[1,2,3]");
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await res.Content.ReadAsStringAsync()).Should().Contain("mảng JSON các chuỗi");
    }

    [Fact]
    public async Task Select_OptionsLaDoiTuong_ThiTuChoi()
    {
        var res = await TaoAsync("Select", "{\"a\":\"b\"}");
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Select_OptionsLaMangRong_ThiTuChoi()
    {
        // Mảng rỗng đọc được nhưng không có lựa chọn nào: ô chọn không bấm được gì.
        var res = await TaoAsync("Select", "[]");
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Select_OptionsLaMangChuoi_ThiTaoDuoc()
    {
        var res = await TaoAsync("Select", "[\"Bolero\",\"Acoustic\"]");
        res.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Range_OptionsLaMang_ThiTuChoi()
    {
        // Range lấy biên từ khoá min/max, nên mảng là khai sai chỗ.
        var res = await TaoAsync("Range", "[1,5]");
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Range_MinLonHonMax_ThiTuChoi()
    {
        // Tiêu chí mà không giá trị nào thoả: người dùng gõ gì cũng bị từ chối, và lỗi hiện ở màn hình
        // sửa buổi diễn chứ không ở chỗ gõ sai.
        var res = await TaoAsync("Range", "{\"min\":5,\"max\":1}");
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await res.Content.ReadAsStringAsync()).Should().Contain("min lớn hơn max");
    }

    [Fact]
    public async Task Range_ChiKhaiStep_VanTaoDuoc()
    {
        // CỐ Ý cho qua: thiếu biên không tạo ra lỗ hổng nào — phép kiểm "phải là số" vẫn chạy. Siết thêm
        // ở đây chỉ chặn một cách khai hợp lệ.
        var res = await TaoAsync("Range", "{\"step\":1}");
        res.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Boolean_GhiChuHoa_ThiDocLaiRaChuThuong()
    {
        var tao = await TaoAsync("Boolean", null);
        tao.StatusCode.Should().Be(HttpStatusCode.Created);
        var criteriaId = (await tao.Content.ReadFromJsonAsync<DataResponse<int>>())!.Data;

        var owner = _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner", SeedHelper.LoungeId);
        var gan = await owner.PostAsJsonAsync(
            $"/api/v1/custom-criteria/shows/{SeedHelper.ShowId}/values",
            new[] { new { CriteriaId = criteriaId, Value = "TRUE" } });
        gan.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var doc = await owner.GetAsync($"/api/v1/custom-criteria/shows/{SeedHelper.ShowId}/values");
        var root = JsonDocument.Parse(await doc.Content.ReadAsStringAsync()).RootElement;
        var data = root.TryGetProperty("data", out var d) ? d : root;

        var mine = data.EnumerateArray()
            .First(v => v.GetProperty("criteriaId").GetInt32() == criteriaId);

        mine.GetProperty("value").GetString().Should().Be("true",
            "ô chọn dựng bằng true/false chữ thường sẽ không khớp được dòng ghi \"TRUE\", rồi lần Lưu sau xoá mất giá trị");
    }
}
