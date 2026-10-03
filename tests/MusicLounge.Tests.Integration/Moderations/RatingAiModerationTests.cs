using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Hangfire;
using Hangfire.Client;
using Hangfire.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Infrastructure.Jobs;
using MusicLounge.Infrastructure.Persistence;
using MusicLounge.Tests.Integration.Fakes;
using MusicLounge.Tests.Integration.Helpers;

namespace MusicLounge.Tests.Integration.Moderations;

/// <summary>
/// MLACP-574: AI chấm lời bình của đánh giá. Luật: lời bình hiện ngay; AI đánh giá High/Critical → ẨN TẠM phần chữ (số
/// sao vẫn tính) + vào hàng đợi báo cáo vi phạm của Admin; Admin "Bỏ qua" → hiện lại, "Gỡ" → gỡ hẳn; AI không trả lời →
/// không đổi gì. AI ở đây là <see cref="FakeAiModerationService"/> (điều khiển bằng dấu trong lời bình).
/// </summary>
[Collection("Integration")]
public sealed class RatingAiModerationTests
{
    private readonly ApiFactory _factory;
    public RatingAiModerationTests(ApiFactory factory) => _factory = factory;

    private HttpClient Admin() => _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin");

    private async Task<Guid> TaoBuoiDaDien()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var show = new LoungeShow
        {
            LoungeId = SeedHelper.LoungeId,
            Name = $"RatingAiShow-{Guid.NewGuid():N}",
            Description = "test",
            Format = LoungeShowFormat.Offline,
            Status = LoungeShowStatus.Ended,
            ScheduledStart = DateTimeOffset.UtcNow.AddDays(-2),
            ActualEnd = DateTimeOffset.UtcNow.AddDays(-2).AddHours(3),
            RatingOpenUntil = DateTimeOffset.UtcNow.AddDays(5),
        };
        db.LoungeShows.Add(show);
        await db.SaveChangesAsync();
        return show.Id;
    }

    private async Task<Guid> ThemDanhGia(Guid showId, Guid userId, int sao, string? loi)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var r = new LoungeShowRating { UserId = userId, LoungeShowId = showId, Score = sao, Comment = loi };
        db.Add(r);
        await db.SaveChangesAsync();
        return r.Id;
    }

    private async Task ChayJob(Guid ratingId)
    {
        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ScoreRatingWithAiJob>()
            .ExecuteAsync(ratingId, new JobCancellationToken(false));
    }

    private async Task<LoungeShowRating> DocDanhGia(Guid ratingId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Set<LoungeShowRating>().AsNoTracking().FirstAsync(r => r.Id == ratingId);
    }

    private async Task<JsonElement> DocCongKhai(Guid showId)
        => (await (await _factory.CreateClient().GetAsync($"/api/v1/lounge-shows/{showId}/ratings"))
            .Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");

    private static JsonElement Dong(JsonElement data, Guid ratingId)
        => data.GetProperty("items").GetProperty("items").EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == ratingId);

    private sealed class GhiJobDuocTao : IClientFilter
    {
        public List<Job> Jobs { get; } = [];
        public void OnCreating(CreatingContext filterContext) { lock (Jobs) Jobs.Add(filterContext.Job); }
        public void OnCreated(CreatedContext filterContext) { }
    }

    [Fact]
    public async Task LoiBinhSach_ChamDiem_VanHien_KhongVaoHangDoi()
    {
        var showId = await TaoBuoiDaDien();
        var id = await ThemDanhGia(showId, SeedHelper.AudienceId, 2, $"Âm thanh tệ, phục vụ chậm, không đáng tiền. {FakeAiModerationService.DauSach}");

        await ChayJob(id);

        var r = await DocDanhGia(id);
        r.AiScore.Should().NotBeNull();
        r.AiRiskLevel.Should().Be(ModerationRiskLevel.Low);
        r.CommentHiddenAt.Should().BeNull("lời chê lịch sự không phải vi phạm");
        Dong(await DocCongKhai(showId), id).GetProperty("comment").GetString().Should().Contain("Âm thanh tệ");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.Set<ContentReport>().AnyAsync(c => c.TargetId == id)).Should().BeFalse();
    }

    [Theory]
    [InlineData(FakeAiModerationService.DauRuiRoCao, ModerationRiskLevel.High)]
    [InlineData(FakeAiModerationService.DauNghiemTrong, ModerationRiskLevel.Critical)]
    public async Task RuiRoCao_AnChu_GiuSao_VaoHangDoiAdmin(string dau, ModerationRiskLevel mucMongDoi)
    {
        var showId = await TaoBuoiDaDien();
        await ThemDanhGia(showId, SeedHelper.StaffId, 5, "Rất hay");
        var id = await ThemDanhGia(showId, SeedHelper.AudienceId, 1, $"lời bình có vấn đề {dau}");

        await ChayJob(id);

        var r = await DocDanhGia(id);
        r.AiRiskLevel.Should().Be(mucMongDoi);
        r.CommentHiddenAt.Should().NotBeNull();
        r.IsRemoved.Should().BeFalse("AI chỉ ẩn TẠM, không tự gỡ hẳn");
        r.Comment.Should().Contain("lời bình có vấn đề", "nguyên văn vẫn lưu để Admin đọc");

        // Công khai: không trả chữ, có cờ; SỐ SAO vẫn tính vào tổng quan (1 + 5) / 2 = 3.
        var data = await DocCongKhai(showId);
        var dong = Dong(data, id);
        dong.GetProperty("comment").ValueKind.Should().Be(JsonValueKind.Null);
        dong.GetProperty("commentHidden").GetBoolean().Should().BeTrue();
        dong.GetProperty("score").GetInt32().Should().Be(1);
        data.GetProperty("totalCount").GetInt32().Should().Be(2);
        data.GetProperty("averageScore").GetDecimal().Should().Be(3m);

        // Hàng đợi Admin có đúng lời bình đó, đọc được NGUYÊN VĂN, lý do do hệ thống ghi.
        var hangDoi = (await (await Admin().GetAsync("/api/v1/content-reports/queue?pageSize=100"))
            .Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data").GetProperty("items").EnumerateArray()
            .Single(x => x.GetProperty("targetId").GetGuid() == id);
        hangDoi.GetProperty("targetType").GetString().Should().Be("Rating");
        hangDoi.GetRawText().Should().Contain("lời bình có vấn đề").And.Contain("AI gắn cờ lời bình");
    }

    [Fact]
    public async Task AdminBoQua_LoiBinhHienLai()
    {
        var showId = await TaoBuoiDaDien();
        var id = await ThemDanhGia(showId, SeedHelper.AudienceId, 3, $"bị AI bắt nhầm {FakeAiModerationService.DauRuiRoCao}");
        await ChayJob(id);
        (await DocDanhGia(id)).CommentHiddenAt.Should().NotBeNull();

        var res = await Admin().PostAsJsonAsync("/api/v1/content-reports/resolve",
            new { TargetType = "Rating", TargetId = id, Action = "Dismissed", Note = "Lời bình ổn" });
        res.StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await DocDanhGia(id)).CommentHiddenAt.Should().BeNull();
        var dong = Dong(await DocCongKhai(showId), id);
        dong.GetProperty("comment").GetString().Should().Contain("bị AI bắt nhầm");
        dong.GetProperty("commentHidden").GetBoolean().Should().BeFalse();

        // Job chạy lại (Hangfire thử lại) KHÔNG ẩn lần hai lời Admin đã cho qua.
        await ChayJob(id);
        (await DocDanhGia(id)).CommentHiddenAt.Should().BeNull();
    }

    [Fact]
    public async Task AdminGo_DanhGiaBiGoHan()
    {
        var showId = await TaoBuoiDaDien();
        var id = await ThemDanhGia(showId, SeedHelper.AudienceId, 1, $"tục tĩu {FakeAiModerationService.DauRuiRoCao}");
        await ChayJob(id);

        var res = await Admin().PostAsJsonAsync("/api/v1/content-reports/resolve",
            new { TargetType = "Rating", TargetId = id, Action = "Removed", Note = "Ngôn từ tục tĩu" });
        res.StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await DocDanhGia(id)).IsRemoved.Should().BeTrue();
        (await DocCongKhai(showId)).GetProperty("totalCount").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task AiKhongTraLoi_LoiBinhVanHien_KhongDoiGi()
    {
        var showId = await TaoBuoiDaDien();
        var id = await ThemDanhGia(showId, SeedHelper.AudienceId, 4, "không có dấu nào nên AI giả trả null");

        await ChayJob(id);

        var r = await DocDanhGia(id);
        r.AiScore.Should().BeNull();
        r.CommentHiddenAt.Should().BeNull();
        Dong(await DocCongKhai(showId), id).GetProperty("comment").GetString().Should().Contain("không có dấu nào");
    }

    [Fact]
    public async Task MucVua_KhongAn()
    {
        var showId = await TaoBuoiDaDien();
        var id = await ThemDanhGia(showId, SeedHelper.AudienceId, 2, $"gay gắt {FakeAiModerationService.DauVua}");
        await ChayJob(id);
        var r = await DocDanhGia(id);
        r.AiRiskLevel.Should().Be(ModerationRiskLevel.Medium);
        r.CommentHiddenAt.Should().BeNull("chỉ High/Critical mới ẩn tạm");
    }

    [Fact]
    public async Task LoiBinhBiAn_KhongLenDanhGiaNoiBatCuaTrangBuoiDien()
    {
        var showId = await TaoBuoiDaDien();
        var id = await ThemDanhGia(showId, SeedHelper.AudienceId, 5, $"lời nổi bật nhưng bị ẩn {FakeAiModerationService.DauRuiRoCao}");
        await ChayJob(id);

        var chiTiet = await (await _factory.CreateClient().GetAsync($"/api/v1/lounge-shows/{showId}")).Content.ReadAsStringAsync();
        chiTiet.Should().NotContain("lời nổi bật nhưng bị ẩn");
    }

    [Fact]
    public async Task GuiDanhGiaCoLoiBinh_DuaJobAiVaoHangDoi_ChiCoSaoThiKhong()
    {
        var showId = await TaoBuoiDaDien();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            foreach (var u in new[] { SeedHelper.AudienceId, SeedHelper.StaffId })
                db.Add(new Ticket
                {
                    Id = Guid.NewGuid(), BuyerId = u, PriceId = SeedHelper.TicketPriceId, TierId = SeedHelper.TicketTierId,
                    ShowId = showId, Status = TicketStatus.Used, PurchaseChannel = PurchaseChannel.Online,
                    CreatedAt = DateTimeOffset.UtcNow.AddDays(-3)
                });
            await db.SaveChangesAsync();
        }

        var ghi = new GhiJobDuocTao();
        GlobalJobFilters.Filters.Add(ghi);
        try
        {
            (await _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience")
                .PostAsJsonAsync($"/api/v1/lounge-shows/{showId}/rate", new { Score = 5, Comment = "Hay lắm" }))
                .IsSuccessStatusCode.Should().BeTrue();
            (await _factory.CreateAuthenticatedClient(SeedHelper.StaffId, "Staff")
                .PostAsJsonAsync($"/api/v1/lounge-shows/{showId}/rate", new { Score = 4, Comment = (string?)null }))
                .IsSuccessStatusCode.Should().BeTrue();
        }
        finally
        {
            GlobalJobFilters.Filters.Remove(ghi);
        }

        Guid idCoLoi;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            idCoLoi = (await db.Set<LoungeShowRating>().FirstAsync(r => r.LoungeShowId == showId && r.UserId == SeedHelper.AudienceId)).Id;
        }
        var jobAi = ghi.Jobs.Where(j => j.Type == typeof(ScoreRatingWithAiJob)).ToList();
        jobAi.Should().ContainSingle("chỉ đánh giá CÓ lời bình mới cần AI chấm");
        jobAi[0].Args[0].Should().Be(idCoLoi);
    }
}
