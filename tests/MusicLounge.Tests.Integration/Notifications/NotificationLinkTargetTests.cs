using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Tests.Integration.Helpers;

namespace MusicLounge.Tests.Integration.Notifications;

/// <summary>
/// MLACP-460. Bấm vào một thông báo phải mở đúng thứ nó nói tới. Hai chỗ không làm được điều đó:
///
/// <list type="number">
/// <item>Kết quả duyệt buổi phát trực tiếp trả <c>Livestream.Id</c>, trong khi mọi đường dẫn của frontend nhận
/// <c>LoungeShow.Id</c> — hai bảng đánh số riêng nên bấm vào sẽ mở nhầm buổi khác hoặc ra trang trống.</item>
/// <item>Cảnh báo quá hạn xử lý báo cáo vi phạm gom theo cặp (loại, mã) nên mã tham chiếu là chuỗi ghép
/// <c>"Livestream:12"</c>; frontend muốn mở đúng nội dung thì phải tự cắt chuỗi, tức là đoán định dạng nội bộ của
/// backend.</item>
/// </list>
/// </summary>
[Collection("Integration")]
public sealed class NotificationLinkTargetTests
{
    private readonly ApiFactory _factory;

    public NotificationLinkTargetTests(ApiFactory factory) => _factory = factory;

    private sealed record IdResponse(bool Success, int Data);

    private async Task<(int ShowId, int LivestreamId)> BuoiPhatChoDuyetAsync()
    {
        int showId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var show = new LoungeShow
            {
                LoungeId = SeedHelper.LoungeId,
                Name = $"LinkTarget-{Guid.NewGuid():N}",
                Description = "test",
                Format = LoungeShowFormat.Online,
                Status = LoungeShowStatus.Published,
                ScheduledStart = DateTimeOffset.UtcNow.AddDays(1)
            };
            db.LoungeShows.Add(show);
            await db.SaveChangesAsync();
            showId = show.Id;
        }

        var staff = _factory.CreateAuthenticatedClient(SeedHelper.StaffId, "Staff", SeedHelper.LoungeId);
        var res = await staff.PostAsJsonAsync("/api/v1/livestreams", new { ShowId = showId });
        res.StatusCode.Should().Be(HttpStatusCode.Created, await res.Content.ReadAsStringAsync());
        var livestreamId = (await res.Content.ReadFromJsonAsync<IdResponse>())!.Data;

        return (showId, livestreamId);
    }

    [Fact]
    public async Task DuyetBuoiPhat_ThongBaoTroToiMaBuoiHoaNhac_KhongPhaiMaBuoiPhat()
    {
        var (showId, livestreamId) = await BuoiPhatChoDuyetAsync();
        livestreamId.Should().NotBe(showId, "tiền đề: hai bảng đánh số riêng nên mã khác nhau — nếu trùng thì bài test này vô nghĩa");

        var admin = _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin");
        var res = await admin.PostAsJsonAsync(
            $"/api/v1/moderations/livestreams/{livestreamId}/review",
            new { Decision = "Approved", ReviewNote = "ok" });
        res.StatusCode.Should().Be(HttpStatusCode.NoContent, await res.Content.ReadAsStringAsync());

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var thongBao = await db.Notifications.AsNoTracking()
            .Where(n => n.ReferenceType == "livestream" && n.Type == NotificationType.ModerationResult)
            .OrderByDescending(n => n.Id)
            .FirstAsync();

        thongBao.ReferenceId.Should().Be(showId.ToString(),
            "đường dẫn của frontend nhận mã buổi hòa nhạc — trả mã buổi phát là dẫn người dùng tới trang trống");
    }

    [Theory]
    [InlineData("Livestream:12", "Livestream", 12)]
    [InlineData("Show:7", "Show", 7)]
    public async Task MaThamChieuGhep_DuocTachSanChoFrontend(string maGhep, string loaiMongDoi, int maMongDoi)
    {
        var id = await ThongBaoAsync("content_report_target", maGhep);

        var dto = await DocThongBaoAsync(id);

        dto.GetProperty("referenceId").GetString().Should().Be(maGhep,
            "chuỗi ghép là khoá chống gửi trùng của job cảnh báo — không được đổi");
        dto.GetProperty("referenceTargetType").GetString().Should().Be(loaiMongDoi);
        dto.GetProperty("referenceTargetId").GetInt32().Should().Be(maMongDoi);
    }

    [Theory]
    [InlineData("show", "42")]           // mã đơn, dạng thường gặp nhất
    [InlineData("content_report_target", "Livestream:khong-phai-so")]
    [InlineData("content_report_target", ":12")]
    public async Task MaThamChieuKhongPhaiDangGhep_ThiHaiTruongTachLaRong(string loai, string ma)
    {
        var id = await ThongBaoAsync(loai, ma);

        var dto = await DocThongBaoAsync(id);

        dto.GetProperty("referenceTargetType").ValueKind.Should().Be(JsonValueKind.Null,
            "thà không có gì còn hơn đưa cho frontend một mảnh chuỗi để nó đem đi ghép URL");
        dto.GetProperty("referenceTargetId").ValueKind.Should().Be(JsonValueKind.Null);
    }

    // ---------- tiện ích ----------

    private async Task<int> ThongBaoAsync(string referenceType, string referenceId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var thongBao = new Notification
        {
            UserId = SeedHelper.AdminId,
            Type = NotificationType.ContentReportSlaBreached,
            Title = "Quá hạn xử lý báo cáo vi phạm",
            Body = "test",
            ReferenceType = referenceType,
            ReferenceId = referenceId,
            IsRead = false,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.Notifications.Add(thongBao);
        await db.SaveChangesAsync();
        return thongBao.Id;
    }

    private async Task<JsonElement> DocThongBaoAsync(int id)
    {
        var admin = _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin");
        var res = await admin.GetAsync("/api/v1/notifications?page=1&pageSize=100");
        res.StatusCode.Should().Be(HttpStatusCode.OK, await res.Content.ReadAsStringAsync());

        var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("data").GetProperty("items").EnumerateArray()
            .Single(x => x.GetProperty("id").GetInt32() == id)
            .Clone();
    }
}
