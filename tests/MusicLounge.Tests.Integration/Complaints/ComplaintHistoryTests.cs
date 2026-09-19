using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Tests.Integration.Helpers;

namespace MusicLounge.Tests.Integration.Complaints;

/// <summary>
/// MLACP-462. Hàng đợi khiếu nại chỉ trả việc CHƯA xử lý xong, nên xử lý xong là khiếu nại biến mất khỏi mọi màn hình:
/// Admin không tra lại được đã quyết gì, cho ai, vì sao — trong khi chính họ phải trả lời nếu người khiếu nại hỏi lại.
/// Giao diện quản trị đã có sẵn bộ lọc "đã xử lý / bị từ chối" nhưng không có dữ liệu để hiện.
/// </summary>
[Collection("Integration")]
public sealed class ComplaintHistoryTests
{
    private readonly ApiFactory _factory;

    public ComplaintHistoryTests(ApiFactory factory) => _factory = factory;

    private HttpClient Admin() => _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin");

    private async Task<int> KhieuNaiAsync(ComplaintStatus trangThai)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var khieuNai = new Complaint
        {
            ComplainantUserId = SeedHelper.AudienceId,
            TargetType = "show",
            TargetId = SeedHelper.ShowId,
            Category = ComplaintCategory.Other,
            Description = $"Khiếu nại kiểm thử {Guid.NewGuid():N}",
            ContactPhone = "0900000000",
            Status = trangThai,
            Resolution = trangThai is ComplaintStatus.Resolved or ComplaintStatus.Rejected ? "Đã xử lý" : null,
            ResolvedAt = trangThai is ComplaintStatus.Resolved or ComplaintStatus.Rejected
                ? DateTimeOffset.UtcNow
                : null,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.Add(khieuNai);
        await db.SaveChangesAsync();
        return khieuNai.Id;
    }

    private async Task<(IReadOnlyList<int> Ids, HttpStatusCode Code, string Body)> DocAsync(string query)
    {
        var res = await Admin().GetAsync($"/api/v1/admin/complaints?{query}");
        var body = await res.Content.ReadAsStringAsync();
        if (res.StatusCode != HttpStatusCode.OK) return ([], res.StatusCode, body);

        using var doc = JsonDocument.Parse(body);
        var ids = doc.RootElement.GetProperty("data").GetProperty("items").EnumerateArray()
            .Select(x => x.GetProperty("id").GetInt32()).ToList();
        return (ids, res.StatusCode, body);
    }

    [Fact]
    public async Task KhongLoc_TraCaKhieuNaiDaXuLyLanDangMo()
    {
        var dangMo = await KhieuNaiAsync(ComplaintStatus.Open);
        var daXuLy = await KhieuNaiAsync(ComplaintStatus.Resolved);

        var (ids, code, _) = await DocAsync("page=1&pageSize=50");

        code.Should().Be(HttpStatusCode.OK);
        ids.Should().Contain(daXuLy, "đây chính là thứ hàng đợi không bao giờ trả về");
        ids.Should().Contain(dangMo, "bỏ trống bộ lọc nghĩa là mọi trạng thái");
    }

    [Fact]
    public async Task LocNhieuTrangThai_ChiTraDungNhungTrangThaiDo()
    {
        var dangMo = await KhieuNaiAsync(ComplaintStatus.Open);
        var daXuLy = await KhieuNaiAsync(ComplaintStatus.Resolved);
        var biTuChoi = await KhieuNaiAsync(ComplaintStatus.Rejected);

        var (ids, code, _) = await DocAsync("status=Resolved&status=Rejected&page=1&pageSize=50");

        code.Should().Be(HttpStatusCode.OK);
        ids.Should().Contain(daXuLy).And.Contain(biTuChoi);
        ids.Should().NotContain(dangMo,
            "giao diện gửi hai giá trị cùng lúc — nhận một giá trị rồi bỏ qua phần còn lại là lọc sai mà không báo");
    }

    [Fact]
    public async Task TrangThaiSaiTen_BaoRoGiaTriHopLe_KhongImLangTraRong()
    {
        var (_, code, body) = await DocAsync("status=DaXuLyXong");

        code.Should().Be(HttpStatusCode.UnprocessableEntity,
            "trả danh sách rỗng sẽ làm người dùng tưởng không có khiếu nại nào");
        body.Should().Contain("Resolved", "câu lỗi phải liệt kê giá trị hợp lệ để người gọi sửa được ngay");
    }

    [Fact]
    public async Task KhongPhaiAdmin_ThiKhongDocDuoc()
    {
        var chuPhongTra = _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner", SeedHelper.LoungeId);

        var res = await chuPhongTra.GetAsync("/api/v1/admin/complaints");

        res.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "khiếu nại chứa mô tả sự việc và số điện thoại người khiếu nại");
    }
}
