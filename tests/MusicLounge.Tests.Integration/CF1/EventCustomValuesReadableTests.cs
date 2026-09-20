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
/// Giá trị tiêu chí riêng của buổi hòa nhạc phải đọc lại được.
///
/// <para>Thao tác ghi là THAY THẾ TOÀN BỘ danh sách. Không có đường đọc thì màn hình sửa không biết buổi
/// diễn đang gắn gì, nên mỗi lần lưu là chủ phòng trà phải nhập lại từ đầu và quên một tiêu chí là mất
/// tiêu chí đó — cùng lớp lỗi "ghi được mà đọc không được", chỉ khác là thiếu hẳn endpoint chứ không phải
/// thiếu một trường.</para>
/// </summary>
[Collection("Integration")]
public sealed class EventCustomValuesReadableTests
{
    private readonly ApiFactory _factory;

    public EventCustomValuesReadableTests(ApiFactory factory) => _factory = factory;

    private static JsonElement Data(string body)
    {
        var root = JsonDocument.Parse(body).RootElement;
        return root.TryGetProperty("data", out var d) ? d : root;
    }

    private async Task<int> TaoTieuChiAsync(string name, bool isActive = true)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var c = new CustomCriteriaEntity
        {
            LoungeId = SeedHelper.LoungeId,
            Name = name,
            Key = $"key_{Guid.NewGuid():N}"[..20],
            DataType = CustomCriteriaDataType.Text,
            IsActive = isActive
        };
        db.Add(c);
        await db.SaveChangesAsync();
        return c.Id;
    }

    [Fact]
    public async Task GanGiaTriRoiDocLai_TraDungGiaTriVaDinhNghiaTieuChi()
    {
        var criteriaId = await TaoTieuChiAsync("Phong cách trình diễn");
        var owner = _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner", SeedHelper.LoungeId);

        var set = await owner.PostAsJsonAsync(
            $"/api/v1/custom-criteria/shows/{SeedHelper.ShowId}/values",
            new[] { new { CriteriaId = criteriaId, Value = "\"Acoustic\"" } });
        set.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var res = await owner.GetAsync($"/api/v1/custom-criteria/shows/{SeedHelper.ShowId}/values");
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        var mine = Data(await res.Content.ReadAsStringAsync()).EnumerateArray()
            .FirstOrDefault(v => v.GetProperty("criteriaId").GetInt32() == criteriaId);
        mine.ValueKind.Should().NotBe(JsonValueKind.Undefined,
            "ghi xong mà không đọc lại được thì lần sửa sau là mất giá trị");

        mine.GetProperty("value").GetString().Should().Be("\"Acoustic\"");
        // Kèm định nghĩa để màn hình dựng đúng ô nhập, không phải gọi thêm một lượt rồi tự ghép.
        mine.GetProperty("name").GetString().Should().Be("Phong cách trình diễn");
        mine.GetProperty("dataType").ValueKind.Should().NotBe(JsonValueKind.Undefined);
        mine.GetProperty("criteriaIsActive").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task ChuPhongTraKhac_KhongDocDuoc()
    {
        // Cùng luật quyền với lệnh ghi: tiêu chí riêng là cách phòng trà tự phân loại buổi diễn của mình.
        var other = _factory.CreateAuthenticatedClient(SeedHelper.OtherOwnerId, "Owner", SeedHelper.OtherLoungeId);
        var res = await other.GetAsync($"/api/v1/custom-criteria/shows/{SeedHelper.ShowId}/values");
        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
