using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.ValueObjects;
using MusicLounge.Tests.Integration.Fakes;
using MusicLounge.Tests.Integration.Helpers;
using MusicLoungeVenue = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Tests.Integration.Moderations;

/// <summary>
/// MLACP-456. Trước đây khán giả thấy một tin nhắn chat vi phạm thì chỉ báo cáo được CẢ buổi livestream — Admin không
/// biết tin nhắn nào, và cách xử lý duy nhất là chấm dứt cả buổi phát. NĐ 147/2024 đòi người dùng báo cáo được nội dung
/// vi phạm và việc gỡ phải có hiệu lực thật, nên test đi trọn: báo cáo → Admin gỡ → tin nhắn biến mất khỏi lịch sử chat
/// và người đang xem được báo.
/// </summary>
[Collection("Integration")]
public sealed class ChatMessageReportTests
{
    private readonly ApiFactory _factory;

    public ChatMessageReportTests(ApiFactory factory) => _factory = factory;

    private HttpClient Admin() => _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin");
    private HttpClient KhanGia() => _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience");

    private RecordingLivestreamHubService Hub =>
        (RecordingLivestreamHubService)_factory.Services.GetRequiredService<ILivestreamHubService>();

    private sealed record Seeded(int LivestreamId, int ViPhamId, int BinhThuongId);

    /// <summary>Một livestream đang phát với hai tin nhắn: một cái sẽ bị báo cáo, một cái phải còn nguyên.</summary>
    private async Task<Seeded> SeedChatAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var owner = new User { Email = $"chat-owner-{Guid.NewGuid():N}@test.com", FullName = "Chu phong tra" };
        db.Users.Add(owner);
        await db.SaveChangesAsync();

        var lounge = new MusicLoungeVenue
        {
            OwnerId = owner.Id, Name = $"Chat-{Guid.NewGuid():N}"[..20], Status = LoungeStatus.Approved,
            Address = new VenueAddress { Street = "1 Test St", District = "1", City = "HCM" }
        };
        db.Lounges.Add(lounge);
        await db.SaveChangesAsync();

        var start = DateTimeOffset.UtcNow.AddHours(-1);
        var show = new LoungeShow
        {
            LoungeId = lounge.Id, Name = $"ChatShow-{Guid.NewGuid():N}", Description = "test",
            Format = LoungeShowFormat.Online, Status = LoungeShowStatus.Ongoing,
            ScheduledStart = start, ScheduledEnd = start.AddHours(3)
        };
        db.Add(show);
        await db.SaveChangesAsync();

        var livestream = new Livestream { LoungeShowId = show.Id, Status = LivestreamStatus.Live, StartedAt = start };
        db.Add(livestream);
        await db.SaveChangesAsync();

        var viPham = new LivestreamChatMessage
        {
            LivestreamId = livestream.Id, UserId = SeedHelper.AudienceId,
            Message = "Tin nhắn vi phạm", SentAt = DateTimeOffset.UtcNow.AddMinutes(-5)
        };
        var binhThuong = new LivestreamChatMessage
        {
            LivestreamId = livestream.Id, UserId = SeedHelper.AudienceId,
            Message = "Hát hay quá", SentAt = DateTimeOffset.UtcNow.AddMinutes(-4)
        };
        db.AddRange(viPham, binhThuong);
        await db.SaveChangesAsync();

        return new Seeded(livestream.Id, viPham.Id, binhThuong.Id);
    }

    private async Task<HttpResponseMessage> BaoCaoAsync(int chatMessageId)
        => await KhanGia().PostAsJsonAsync("/api/v1/content-reports",
            new { TargetType = "ChatMessage", TargetId = chatMessageId, Reason = "Lời lẽ xúc phạm người khác" });

    private async Task<HttpResponseMessage> GoAsync(int chatMessageId, string note = "Gỡ theo báo cáo vi phạm")
        => await Admin().PostAsJsonAsync("/api/v1/content-reports/resolve",
            new { TargetType = "ChatMessage", TargetId = chatMessageId, Action = "Removed", Note = note });

    private async Task<List<int>> LichSuChatAsync(int livestreamId)
    {
        var res = await KhanGia().GetAsync($"/api/v1/livestreams/{livestreamId}/chat?page=1&pageSize=50");
        res.StatusCode.Should().Be(HttpStatusCode.OK, await res.Content.ReadAsStringAsync());
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("data").GetProperty("items").EnumerateArray()
            .Select(x => x.GetProperty("messageId").GetInt32()).ToList();
    }

    [Fact]
    public async Task BaoCaoTinNhanChat_RoiAdminGo_ThiTinNhanBienMatKhoiLichSu()
    {
        var seeded = await SeedChatAsync();
        (await LichSuChatAsync(seeded.LivestreamId)).Should().Contain(seeded.ViPhamId, "tiền đề: tin nhắn đang hiển thị");

        (await BaoCaoAsync(seeded.ViPhamId)).StatusCode.Should().Be(HttpStatusCode.Created);
        (await GoAsync(seeded.ViPhamId)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var conLai = await LichSuChatAsync(seeded.LivestreamId);
        conLai.Should().NotContain(seeded.ViPhamId, "người vào xem trễ không được đọc lại tin nhắn đã bị gỡ");
        conLai.Should().Contain(seeded.BinhThuongId, "chỉ gỡ đúng tin nhắn bị báo cáo, không đụng tin nhắn khác");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var goRoi = await db.Set<LivestreamChatMessage>().SingleAsync(m => m.Id == seeded.ViPhamId);
        goRoi.IsRemoved.Should().BeTrue();
        goRoi.RemovedReason.Should().Be("Gỡ theo báo cáo vi phạm", "gỡ mềm, giữ bản ghi và lý do để đối chiếu");
    }

    [Fact]
    public async Task AdminGo_ThiNguoiDangXemDuocBaoNgay()
    {
        var seeded = await SeedChatAsync();
        await BaoCaoAsync(seeded.ViPhamId);

        (await GoAsync(seeded.ViPhamId)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        Hub.For(seeded.LivestreamId)
            .Where(s => s.Event == "ChatMessageHidden")
            .Select(s => (int)s.Payload!)
            .Should().ContainSingle().Which.Should().Be(seeded.ViPhamId);
    }

    [Fact]
    public async Task GoHaiLan_ThiBaoDaGoRoi()
    {
        var seeded = await SeedChatAsync();
        await BaoCaoAsync(seeded.ViPhamId);
        (await GoAsync(seeded.ViPhamId)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        await BaoCaoAsync(seeded.ViPhamId);
        (await GoAsync(seeded.ViPhamId)).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    /// <summary>
    /// Hàng đợi báo cáo gom theo NỘI DUNG, mỗi dòng chỉ có mã và số lần bị báo cáo. Với tin nhắn chat, một con số mã thì
    /// Admin không có căn cứ nào để quyết gỡ hay bỏ qua — phải đọc được chính tin nhắn, ai gửi, và mở được buổi hòa nhạc.
    /// </summary>
    [Fact]
    public async Task HangDoiBaoCao_ChoAdminDocDuocNoiDungTinNhanVaMoDuocBuoiHoaNhac()
    {
        var seeded = await SeedChatAsync();
        await BaoCaoAsync(seeded.ViPhamId);

        int showId;
        string tenNguoiGui;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            showId = (await db.Set<Livestream>().SingleAsync(l => l.Id == seeded.LivestreamId)).LoungeShowId;
            tenNguoiGui = (await db.Users.SingleAsync(u => u.Id == SeedHelper.AudienceId)).FullName;
        }

        var res = await Admin().GetAsync("/api/v1/content-reports/queue?page=1&pageSize=100");
        res.StatusCode.Should().Be(HttpStatusCode.OK, await res.Content.ReadAsStringAsync());
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        var dong = doc.RootElement.GetProperty("data").GetProperty("items").EnumerateArray()
            .Single(x => x.GetProperty("targetType").GetString() == "ChatMessage"
                         && x.GetProperty("targetId").GetInt32() == seeded.ViPhamId);

        dong.GetProperty("targetSummary").GetString()
            .Should().Contain("Tin nhắn vi phạm", "Admin phải đọc được chính nội dung bị báo cáo")
            .And.Contain(tenNguoiGui, "kèm người gửi");
        dong.GetProperty("showId").GetInt32().Should().Be(showId, "để mở được ngữ cảnh buổi hòa nhạc");
    }

    [Fact]
    public async Task BaoCaoTinNhanKhongTonTai_Tra404_BangTiengViet()
    {
        var res = await BaoCaoAsync(999_999_999);

        res.StatusCode.Should().Be(HttpStatusCode.NotFound);
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("message").GetString()
            .Should().Be("Không tìm thấy tin nhắn chat (mã 999999999).");
    }
}
