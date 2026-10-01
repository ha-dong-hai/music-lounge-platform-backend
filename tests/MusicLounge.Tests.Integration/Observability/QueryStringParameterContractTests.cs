using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Reflection;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;

namespace MusicLounge.Tests.Integration.Observability;

/// <summary>
/// MLACP-519 (M-387, DEF-BE-06/07 — tab test đo trên Azure). Dự án bật Nullable, nên ASP.NET Core coi một tham số query
/// <c>string x</c> (không dấu ?) là BẮT BUỘC và trả 400 ngay ở model binding. Swashbuckle thì KHÔNG suy ra điều đó từ
/// nullability — hợp đồng ghi "tuỳ chọn". Hậu quả đã đo: <c>/lounge-shows/suggestions</c> thiếu q → 400 dù code có nhánh
/// "q rỗng → danh sách rỗng" (nhánh đó không bao giờ chạy), và client làm theo hợp đồng thì bị 400 không báo trước.
/// Luật: tham số query kiểu string hoặc khai <c>string?</c> (tuỳ chọn thật), hoặc gắn <c>[Required]</c> (để hợp đồng nói đúng).
/// </summary>
[Collection("Integration")]
public sealed class QueryStringParameterContractTests
{
    private readonly ApiFactory _factory;

    public QueryStringParameterContractTests(ApiFactory factory) => _factory = factory;

    [Theory]
    [InlineData("")]            // thiếu hẳn q
    [InlineData("q=")]          // ô gợi ý vừa xoá hết chữ
    [InlineData("q=%20%20")]    // chỉ có khoảng trắng
    public async Task GoiY_KhongCoChu_TraDanhSachRong_KhongBao400(string query)
    {
        var res = await _factory.CreateClient().GetAsync($"/api/v1/lounge-shows/suggestions?{query}");

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("data").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task HopDongSwagger_KhaiDungThamSoBatBuoc()
    {
        var res = await _factory.CreateClient().GetAsync("/swagger/v1/swagger.json");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        var paths = doc.RootElement.GetProperty("paths");

        static bool BatBuoc(JsonElement op, string ten) => op.GetProperty("parameters").EnumerateArray()
            .Single(p => p.GetProperty("name").GetString() == ten)
            .TryGetProperty("required", out var r) && r.GetBoolean();

        BatBuoc(paths.GetProperty("/api/v1/lounge-shows/suggestions").GetProperty("get"), "q").Should().BeFalse();
        BatBuoc(paths.GetProperty("/api/v1/lounges/staff/lookup").GetProperty("get"), "email").Should().BeTrue(
            "thiếu email thì 400 — hợp đồng phải báo trước điều đó");
    }

    [Fact]
    public void MoiThamSoQueryKieuString_HoacChoPhepNull_HoacGanRequired()
    {
        var nullability = new NullabilityInfoContext();
        var thamSo = typeof(Api.Controllers.LoungeShowsController).Assembly.GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract)
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .SelectMany(m => m.GetParameters().Select(p => (m, p)))
            .Where(x => x.p.ParameterType == typeof(string) && x.p.GetCustomAttribute<FromQueryAttribute>() is not null)
            .ToList();

        thamSo.Should().NotBeEmpty("phép quét phải chạm ít nhất một tham số, nếu không nó xanh vì không tìm thấy gì");

        var batBuocNgam = thamSo
            .Where(x => nullability.Create(x.p).ReadState == NullabilityState.NotNull
                        && !x.p.HasDefaultValue
                        && x.p.GetCustomAttribute<RequiredAttribute>() is null)
            .Select(x => $"{x.m.DeclaringType!.Name}.{x.m.Name}({x.p.Name})")
            .ToList();

        batBuocNgam.Should().BeEmpty(
            "tham số này bị ASP.NET Core bắt buộc ngầm (400 khi thiếu) nhưng hợp đồng Swagger ghi tuỳ chọn — " +
            "khai `string?` nếu thật sự tuỳ chọn, hoặc gắn [Required] nếu bắt buộc");
    }
}
