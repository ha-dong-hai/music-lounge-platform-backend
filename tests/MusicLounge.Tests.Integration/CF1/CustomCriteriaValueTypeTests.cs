using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MusicLounge.Domain.Enums;
using MusicLounge.Infrastructure.Persistence;
using MusicLounge.Tests.Integration.Helpers;
using CustomCriteriaEntity = MusicLounge.Domain.Entities.CustomCriteria;

namespace MusicLounge.Tests.Integration.CF1;

/// <summary>
/// Giá trị gắn cho buổi hòa nhạc phải khớp kiểu dữ liệu mà chính tiêu chí đó khai.
///
/// <para>Trước đây máy chủ nhận mọi chuỗi: tiêu chí kiểu Boolean vẫn ghi được chữ bất kỳ. Kiểm ở phía
/// giao diện chỉ là lớp tiện cho người dùng — gọi thẳng giao diện lập trình là ghi rác vào được, và rác
/// trong cột giá trị thì mọi màn hình đọc lên đều phải chịu.</para>
/// </summary>
[Collection("Integration")]
public sealed class CustomCriteriaValueTypeTests
{
    private readonly ApiFactory _factory;

    public CustomCriteriaValueTypeTests(ApiFactory factory) => _factory = factory;

    private async Task<int> TaoTieuChiAsync(CustomCriteriaDataType dataType, string? options = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var c = new CustomCriteriaEntity
        {
            LoungeId = SeedHelper.LoungeId,
            Name = $"TC {dataType} {Guid.NewGuid():N}"[..20],
            Key = $"k{Guid.NewGuid():N}"[..12],
            DataType = dataType,
            Options = options,
            IsActive = true
        };
        db.Add(c);
        await db.SaveChangesAsync();
        return c.Id;
    }

    private async Task<HttpResponseMessage> GanAsync(int criteriaId, string value)
    {
        var owner = _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner", SeedHelper.LoungeId);
        return await owner.PostAsJsonAsync(
            $"/api/v1/custom-criteria/shows/{SeedHelper.ShowId}/values",
            new[] { new { CriteriaId = criteriaId, Value = value } });
    }

    [Fact]
    public async Task Boolean_ChiNhanTrueFalse()
    {
        var id = await TaoTieuChiAsync(CustomCriteriaDataType.Boolean);

        (await GanAsync(id, "true")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await GanAsync(id, "FALSE")).StatusCode.Should().Be(HttpStatusCode.NoContent, "không phân biệt hoa thường");

        var hong = await GanAsync(id, "khong-phai-bool");
        hong.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "tiêu chí khai kiểu Boolean mà nhận chữ bất kỳ thì màn hình nào đọc lên cũng phải chịu");
        (await hong.Content.ReadAsStringAsync()).Should().Contain("true");
    }

    [Fact]
    public async Task Boolean_VanNhanGiaTriCuDangJson()
    {
        // Dữ liệu ghi trước đây có thể ở dạng JSON đã đóng gói. Ràng buộc mới không được từ chối đúng
        // thứ mà chính hệ thống từng sinh ra.
        var id = await TaoTieuChiAsync(CustomCriteriaDataType.Boolean);
        (await GanAsync(id, "\"true\"")).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Range_PhaiLaSo_VaNamTrongKhoang()
    {
        var id = await TaoTieuChiAsync(CustomCriteriaDataType.Range, "{\"min\":1,\"max\":5}");

        (await GanAsync(id, "3")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await GanAsync(id, "1")).StatusCode.Should().Be(HttpStatusCode.NoContent, "biên dưới vẫn hợp lệ");
        (await GanAsync(id, "5")).StatusCode.Should().Be(HttpStatusCode.NoContent, "biên trên vẫn hợp lệ");

        (await GanAsync(id, "abc")).StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await GanAsync(id, "0")).StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, "dưới khoảng");
        (await GanAsync(id, "6")).StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, "trên khoảng");
    }

    [Fact]
    public async Task Range_KhongDocDuocKhoang_ThiChiDoiLaSo()
    {
        // Dòng cũ tạo trước khi lệnh tạo tiêu chí bắt buộc Options là JSON hợp lệ. Từ chối dựa trên một
        // quy tắc mình không đọc được còn tệ hơn là cho qua.
        var id = await TaoTieuChiAsync(CustomCriteriaDataType.Range, "khong-phai-json");

        (await GanAsync(id, "42")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await GanAsync(id, "abc")).StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "kiểu đã là Range thì vẫn phải là số, kể cả khi không biết khoảng cho phép");
    }

    [Fact]
    public async Task Select_PhaiNamTrongDanhSachLuaChon()
    {
        var id = await TaoTieuChiAsync(CustomCriteriaDataType.Select, "[\"Acoustic\",\"Bolero\"]");

        (await GanAsync(id, "Bolero")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var hong = await GanAsync(id, "Rock");
        hong.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await hong.Content.ReadAsStringAsync()).Should().Contain("Acoustic",
            "thông điệp phải nói rõ được chọn những gì, không chỉ nói là sai");
    }

    [Fact]
    public async Task Text_KhongBiRangBuocThemGiNgoaiDoDai()
    {
        var id = await TaoTieuChiAsync(CustomCriteriaDataType.Text);
        (await GanAsync(id, "Bất kỳ chữ gì cũng được")).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}
