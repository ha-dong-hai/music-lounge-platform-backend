using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Infrastructure.Persistence;
using MusicLounge.Tests.Integration.Helpers;

namespace MusicLounge.Tests.Integration.Admin;

/// <summary>
/// MLACP-617. Huy hieu tren menu Admin phai noi dung so viec dang cho cua tung trang, va so viec da qua han phai theo
/// dung moc cua cac job canh bao.
/// </summary>
[Collection("Integration")]
public sealed class AdminWorkQueueTests
{
    private readonly ApiFactory _factory;

    public AdminWorkQueueTests(ApiFactory factory) => _factory = factory;

    private HttpClient Admin() => _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin");

    private async Task<Dictionary<string, JsonElement>> HangDoiAsync()
    {
        var res = await Admin().GetAsync("/api/v1/admin/work-queue");
        res.StatusCode.Should().Be(HttpStatusCode.OK, await res.Content.ReadAsStringAsync());
        return (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data").EnumerateArray()
            .ToDictionary(e => e.GetProperty("key").GetString()!, e => e);
    }

    private async Task<int> TongDongAsync(string url)
    {
        var res = await Admin().GetAsync(url);
        res.StatusCode.Should().Be(HttpStatusCode.OK, $"{url}: {await res.Content.ReadAsStringAsync()}");
        return (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data").GetProperty("totalCount").GetInt32();
    }

    [Fact]
    public async Task SoTrenMoiHangDoi_BangDungSoDongCuaTrangDanhSach()
    {
        // Them it nhat mot viec vao mot hang doi de phep so khong xanh vi moi thu deu bang 0. Them ca mot yeu cau DA DUYET:
        // neu khong, chay rieng lop nay thi "dem moi yeu cau" tinh co bang "dem yeu cau dang cho" va phep so khong phan
        // biet duoc hai dinh nghia (do bang dot bien ngay 04/10/2026).
        await ThemYeuCauHoanAsync(DateTimeOffset.UtcNow.AddHours(-1));
        await ThemYeuCauHoanAsync(DateTimeOffset.UtcNow.AddHours(-2), RefundRequestStatus.Approved);

        var hd = await HangDoiAsync();
        var trang = new Dictionary<string, string>
        {
            ["shows"] = "/api/v1/admin/shows/pending?pageSize=1",
            ["venues"] = "/api/v1/admin/venues/pending?pageSize=1",
            ["kyc-reviews"] = "/api/v1/admin/kyc-reviews?pageSize=1",
            ["content-reports"] = "/api/v1/content-reports/queue?pageSize=1",
            ["refunds"] = "/api/v1/admin/refund-requests?pageSize=1",
            ["settlements"] = "/api/v1/admin/settlements/pending-review?pageSize=1",
            ["bank-accounts"] = "/api/v1/admin/bank-accounts?pageSize=1",
            ["complaint"] = "/api/v1/admin/complaints?status=Open&status=Investigating&pageSize=1",
            ["penalty-appeals"] = "/api/v1/venue-penalties/appeals?pageSize=1",
        };

        hd.Keys.Should().BeEquivalentTo(trang.Keys, "mỗi hàng đợi trên menu có đúng một mục");
        hd["refunds"].GetProperty("count").GetInt32().Should().BeGreaterThan(0, "chặn trường hợp so khớp toàn số 0");
        foreach (var (key, url) in trang)
            hd[key].GetProperty("count").GetInt32().Should().Be(await TongDongAsync(url), $"huy hiệu '{key}' phải bằng số dòng của trang");
    }

    [Fact]
    public async Task YeuCauHoanChoQua72Gio_TinhLaQuaHan_HangDoiKhongCoHanTraNull()
    {
        var truoc = (await HangDoiAsync())["refunds"].GetProperty("overdueCount").GetInt32();

        await ThemYeuCauHoanAsync(DateTimeOffset.UtcNow.AddHours(-100));
        var hd = await HangDoiAsync();

        hd["refunds"].GetProperty("overdueCount").GetInt32().Should().Be(truoc + 1,
            "chờ 100 giờ thì đã quá thời hạn 72 giờ của hoàn tiền");
        hd["venues"].GetProperty("overdueCount").ValueKind.Should().Be(JsonValueKind.Null,
            "duyệt phòng trà chưa có thời hạn cam kết nào — không được tự đặt");
    }

    [Fact]
    public async Task YeuCauHoanMoi_HanGanNhatLaTaoLucCong72Gio()
    {
        // Han gan nhat = han NHO NHAT con trong tuong lai. Yeu cau tao 71 gio truoc con ~1 gio — nho hon han cua moi yeu
        // cau cho khac trong du lieu test (cac lop khac tao yeu cau "vua xong", con ~72 gio).
        await ThemYeuCauHoanAsync(DateTimeOffset.UtcNow.AddHours(-71));
        var hd = await HangDoiAsync();

        var han = hd["refunds"].GetProperty("nextDueAt").GetDateTimeOffset();
        han.Should().BeAfter(DateTimeOffset.UtcNow).And.BeBefore(DateTimeOffset.UtcNow.AddHours(1).AddMinutes(1),
            "yêu cầu tạo 71 giờ trước còn khoảng 1 giờ tới hạn 72 giờ");
    }

    [Fact]
    public async Task NguoiKhongPhaiAdmin_BiChan()
    {
        var res = await _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner").GetAsync("/api/v1/admin/work-queue");
        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private async Task ThemYeuCauHoanAsync(DateTimeOffset taoLuc, RefundRequestStatus trangThai = RefundRequestStatus.Pending)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var payment = new Payment
        {
            OrderId = $"WQ-{Guid.NewGuid():N}"[..30], GrossAmount = 100_000m, NetAmount = 90_000m,
            Method = PaymentMethod.Gateway, Status = PaymentStatus.Confirmed, TransactionId = $"WQ{Guid.NewGuid():N}"[..16],
            ReferenceType = "TicketHold", ReferenceId = "0", PaidAt = taoLuc.AddHours(-1), CreatedAt = taoLuc.AddHours(-1)
        };
        db.Payments.Add(payment);
        await db.SaveChangesAsync();
        db.RefundRequests.Add(new RefundRequest
        {
            PaymentId = payment.Id, RequestedBy = SeedHelper.AudienceId, Reason = "Khách huỷ vé",
            AmountRequested = 100_000m, RefundPercentage = 100m, Status = trangThai
        });
        await db.SaveChangesAsync();
        // CreatedAt do DbContext tu dong ghi bang gio hien tai khi luu — dat lai sau khi luu de mo phong yeu cau cu.
        var r = db.RefundRequests.Single(x => x.PaymentId == payment.Id);
        r.CreatedAt = taoLuc.UtcDateTime;
        await db.SaveChangesAsync();
    }
}
