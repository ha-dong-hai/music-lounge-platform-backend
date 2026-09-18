using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using MusicLounge.Tests.Integration.Helpers;

namespace MusicLounge.Tests.Integration.Common;

/// <summary>
/// MLACP-448. Mọi phản hồi lỗi phải cùng một hình dạng <c>{success:false, message, errors}</c> và câu tiếng Việt.
/// Trước đây 401/403 của middleware phân quyền trả body rỗng, và hai câu lỗi chung là tiếng Anh.
///
/// Giới hạn đã biết: môi trường test dùng <c>TestAuthHandler</c> thay cho JwtBearer, và handler này không đặt header
/// <c>WWW-Authenticate</c> — nên việc header đó vẫn còn trên production (RFC 6750) không kiểm được ở đây; phải probe
/// sau khi deploy. Phần body thì không phụ thuộc scheme, vì nó được ghi SAU khi scheme đã xử lý xong.
/// </summary>
[Collection("Integration")]
public sealed class ErrorResponseShapeTests
{
    private const string EndpointChiAdmin = "/api/v1/analytics/platform";
    private readonly ApiFactory _factory;

    public ErrorResponseShapeTests(ApiFactory factory) => _factory = factory;

    private static async Task<(bool Success, string? Message, JsonElement Errors)> DocBodyAsync(HttpResponseMessage res)
    {
        var raw = await res.Content.ReadAsStringAsync();
        raw.Should().NotBeNullOrWhiteSpace("phản hồi lỗi không được có body rỗng");
        res.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
        using var doc = JsonDocument.Parse(raw);
        var r = doc.RootElement;
        return (r.GetProperty("success").GetBoolean(), r.GetProperty("message").GetString(), r.GetProperty("errors").Clone());
    }

    [Fact]
    public async Task ChuaDangNhap_401_CoBodyJson()
    {
        var res = await _factory.CreateClient().GetAsync(EndpointChiAdmin);

        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var (success, message, _) = await DocBodyAsync(res);
        success.Should().BeFalse();
        message.Should().Be("Bạn cần đăng nhập để thực hiện thao tác này.");
    }

    [Fact]
    public async Task CoTokenNhungHong_401_BaoPhienHetHan()
    {
        // Frontend dựa vào khác biệt này để làm mới phiên thay vì đẩy người dùng về trang đăng nhập.
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", "token-hong-hoac-het-han");

        var res = await client.GetAsync(EndpointChiAdmin);

        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var (_, message, _) = await DocBodyAsync(res);
        message.Should().Be("Phiên đăng nhập không hợp lệ hoặc đã hết hạn. Vui lòng đăng nhập lại.");
    }

    [Fact]
    public async Task SaiVaiTro_403_CoBodyJson()
    {
        var owner = _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner", SeedHelper.LoungeId);

        var res = await owner.GetAsync(EndpointChiAdmin);

        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var (success, message, _) = await DocBodyAsync(res);
        success.Should().BeFalse();
        message.Should().Be("Bạn không có quyền thực hiện thao tác này.");
    }

    [Fact]
    public async Task DungVaiTro_KhongBiAnhHuong()
    {
        var admin = _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin");

        var res = await admin.GetAsync(EndpointChiAdmin);

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("success").GetBoolean().Should().BeTrue("request hợp lệ phải đi tiếp bình thường");
    }

    [Fact]
    public async Task LoiKiemDuLieu_FluentValidation_CauTiengViet()
    {
        // UpdateSystemConfigCommandValidator bắt buộc lý do dài ít nhất 10 ký tự.
        var admin = _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin");

        var res = await admin.PutAsJsonAsync("/api/v1/admin/system-config/ticket_hold_minutes",
            new { ConfigValue = "20", Note = "" });

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var (success, message, errors) = await DocBodyAsync(res);
        success.Should().BeFalse();
        message.Should().Be("Dữ liệu gửi lên không hợp lệ.");
        errors.ValueKind.Should().Be(JsonValueKind.Object, "chi tiết từng trường vẫn phải có");
    }

    [Fact]
    public async Task LoiRangBuocModel_JsonHong_CauTiengViet()
    {
        var admin = _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin");
        var jsonHong = new StringContent("{ \"ConfigValue\": ", Encoding.UTF8, "application/json");

        var res = await admin.PutAsync("/api/v1/admin/system-config/ticket_hold_minutes", jsonHong);

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var (_, message, _) = await DocBodyAsync(res);
        message.Should().Be("Dữ liệu gửi lên không hợp lệ.");
    }
}
