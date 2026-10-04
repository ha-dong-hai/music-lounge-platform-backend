using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Infrastructure.Persistence;
using MusicLounge.Tests.Integration.Helpers;

namespace MusicLounge.Tests.Integration.CF1;

/// <summary>
/// MLACP-633. Hai lệnh bắt đầu (buổi diễn tại chỗ, phát livestream) trước đây không kiểm giờ: bấm lúc nào
/// cũng chuyển sang Ongoing. Chủ dự án quyết 04/10/2026: giữ cho bắt đầu sớm, chỉ chặn khi lịch đã qua giờ
/// kết thúc. Đồng thời danh sách và trang chi tiết phải trả giờ kết thúc hiệu lực để giao diện hiện rõ
/// "bắt đầu – kết thúc".
///
/// Buổi diễn ở đây đặt ở phòng trà phụ (OtherLoungeId) và quanh giờ hiện tại — khác dải giờ tương lai mà
/// SeedHelper.NextShowStart() cấp cho test khác ở phòng trà chính — và bị xoá trong finally để không để lại
/// buổi Published đã quá giờ cho các test quét job tự kết thúc buổi diễn.
/// </summary>
[Collection("Integration")]
public sealed class ShowStartAfterEndTests
{
    private const string PastEndMessage = "Buổi diễn đã qua giờ kết thúc theo lịch nên không thể bắt đầu nữa.";
    private readonly ApiFactory _factory;

    public ShowStartAfterEndTests(ApiFactory factory) => _factory = factory;

    private HttpClient OtherVenueStaff()
        => _factory.CreateAuthenticatedClient(SeedHelper.OtherVenueStaffId, "Staff", SeedHelper.OtherLoungeId);

    private async Task<Guid> SeedShowAsync(
        DateTimeOffset start, DateTimeOffset? end,
        LoungeShowFormat format = LoungeShowFormat.Offline)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var show = new LoungeShow
        {
            LoungeId = SeedHelper.OtherLoungeId,
            Name = $"StartAfterEnd-{Guid.NewGuid():N}",
            Description = "test",
            Format = format,
            Status = LoungeShowStatus.Published,
            ScheduledStart = start,
            ScheduledEnd = end,
            VcpmcRoyaltyReference = "VCPMC-TEST"
        };
        db.LoungeShows.Add(show);
        await db.SaveChangesAsync();
        return show.Id;
    }

    private async Task<LoungeShow> ReloadAsync(Guid showId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return (await db.LoungeShows.FindAsync(showId))!;
    }

    private async Task DeleteShowAsync(Guid showId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Livestreams.RemoveRange(db.Livestreams.Where(l => l.LoungeShowId == showId));
        var show = await db.LoungeShows.FindAsync(showId);
        if (show is not null) db.LoungeShows.Remove(show);
        await db.SaveChangesAsync();
    }

    private static async Task<string?> ErrorMessageAsync(HttpResponseMessage res)
    {
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        return doc.RootElement.TryGetProperty("message", out var m) ? m.GetString() : null;
    }

    [Fact]
    public async Task StartOfflineShow_PastScheduledEnd_Returns422_AndStaysPublished()
    {
        var now = DateTimeOffset.UtcNow;
        var showId = await SeedShowAsync(now.AddHours(-3), now.AddMinutes(-5));
        try
        {
            var res = await OtherVenueStaff().PostAsync($"/api/v1/lounge-shows/{showId}/start", null);

            res.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
            (await ErrorMessageAsync(res)).Should().Be(PastEndMessage);
            var show = await ReloadAsync(showId);
            show.Status.Should().Be(LoungeShowStatus.Published);
            show.ActualStart.Should().BeNull();
        }
        finally { await DeleteShowAsync(showId); }
    }

    [Fact]
    public async Task StartOfflineShow_NoScheduledEnd_UsesFourHourDefault()
    {
        // Không khai giờ kết thúc → hệ thống tính bắt đầu + 4 tiếng (ShowSchedule.DefaultDurationHours).
        var now = DateTimeOffset.UtcNow;
        var pastId = await SeedShowAsync(now.AddHours(-5), end: null);
        var runningId = await SeedShowAsync(now.AddHours(-3), end: null);
        try
        {
            var past = await OtherVenueStaff().PostAsync($"/api/v1/lounge-shows/{pastId}/start", null);
            past.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

            var running = await OtherVenueStaff().PostAsync($"/api/v1/lounge-shows/{runningId}/start", null);
            running.StatusCode.Should().Be(HttpStatusCode.NoContent);
        }
        finally
        {
            await DeleteShowAsync(pastId);
            await DeleteShowAsync(runningId);
        }
    }

    [Fact]
    public async Task StartOfflineShow_BeforeScheduledStart_StillAllowed()
    {
        // Chủ dự án giữ nguyên việc cho bắt đầu sớm — test này giữ quyết định đó khỏi bị siết nhầm.
        var now = DateTimeOffset.UtcNow;
        var showId = await SeedShowAsync(now.AddHours(5), now.AddHours(7));
        try
        {
            var res = await OtherVenueStaff().PostAsync($"/api/v1/lounge-shows/{showId}/start", null);

            res.StatusCode.Should().Be(HttpStatusCode.NoContent);
            (await ReloadAsync(showId)).Status.Should().Be(LoungeShowStatus.Ongoing);
        }
        finally { await DeleteShowAsync(showId); }
    }

    [Fact]
    public async Task StartLivestream_PastScheduledEnd_Returns422_AndStaysScheduled()
    {
        var now = DateTimeOffset.UtcNow;
        // Tạo và duyệt livestream khi buổi còn ở tương lai, rồi lùi lịch về quá khứ — mô phỏng buổi đã hết giờ
        // mà người vận hành chưa từng bấm phát.
        var showId = await SeedShowAsync(now.AddDays(2), now.AddDays(2).AddHours(2), LoungeShowFormat.Online);
        try
        {
            var staff = OtherVenueStaff();
            var createRes = await staff.PostAsJsonAsync("/api/v1/livestreams", new { ShowId = showId });
            createRes.EnsureSuccessStatusCode();
            using var created = JsonDocument.Parse(await createRes.Content.ReadAsStringAsync());
            var livestreamId = created.RootElement.GetProperty("data").GetGuid();

            var admin = _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin");
            (await admin.PostAsJsonAsync($"/api/v1/moderations/livestreams/{livestreamId}/review",
                new { Decision = "Approved", ReviewNote = "OK" })).EnsureSuccessStatusCode();

            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var show = (await db.LoungeShows.FindAsync(showId))!;
                show.ScheduledStart = now.AddHours(-3);
                show.ScheduledEnd = now.AddMinutes(-5);
                await db.SaveChangesAsync();
            }

            var res = await staff.PostAsync($"/api/v1/livestreams/{livestreamId}/start", null);

            res.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
            (await ErrorMessageAsync(res)).Should().Be(PastEndMessage);
            (await ReloadAsync(showId)).Status.Should().Be(LoungeShowStatus.Published);
            using var verify = _factory.Services.CreateScope();
            var vdb = verify.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            (await vdb.Livestreams.FindAsync(livestreamId))!.Status.Should().Be(LivestreamStatus.Scheduled);
        }
        finally { await DeleteShowAsync(showId); }
    }

    [Fact]
    public async Task ListAndDetail_ReturnEffectiveEnd_EvenWhenScheduledEndIsEmpty()
    {
        var start = new DateTimeOffset(DateTimeOffset.UtcNow.AddDays(30).Date, TimeSpan.Zero).AddHours(12);
        var withEnd = await SeedShowAsync(start, start.AddHours(2).AddMinutes(30));
        var noEnd = await SeedShowAsync(start.AddDays(1), end: null);
        try
        {
            var anon = _factory.CreateClient();

            async Task<DateTimeOffset> DetailEnd(Guid id)
            {
                using var d = JsonDocument.Parse(await anon.GetStringAsync($"/api/v1/lounge-shows/{id}"));
                return d.RootElement.GetProperty("data").GetProperty("effectiveEnd").GetDateTimeOffset();
            }
            (await DetailEnd(withEnd)).Should().Be(start.AddHours(2).AddMinutes(30));
            (await DetailEnd(noEnd)).Should().Be(start.AddDays(1).AddHours(4));

            var owner = _factory.CreateAuthenticatedClient(SeedHelper.OtherOwnerId, "Owner");
            using var list = JsonDocument.Parse(
                await owner.GetStringAsync("/api/v1/lounge-shows?mine=true&pageSize=100"));
            var items = list.RootElement.GetProperty("data").GetProperty("items").EnumerateArray()
                .ToDictionary(i => i.GetProperty("id").GetGuid(), i => i.GetProperty("effectiveEnd").GetDateTimeOffset());
            items.Should().ContainKey(withEnd).And.ContainKey(noEnd);   // chặn "quét trúng số không"
            items[withEnd].Should().Be(start.AddHours(2).AddMinutes(30));
            items[noEnd].Should().Be(start.AddDays(1).AddHours(4));
        }
        finally
        {
            await DeleteShowAsync(withEnd);
            await DeleteShowAsync(noEnd);
        }
    }
}
