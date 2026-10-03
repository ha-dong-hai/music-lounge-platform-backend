using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Infrastructure.Persistence;
using MusicLounge.Tests.Integration.Helpers;

namespace MusicLounge.Tests.Integration.LoungeShows;

/// <summary>MLACP-573: GET /lounge-shows/{id}/ratings?score= — lọc danh sách nhận xét theo số sao. Tổng quan (điểm trung
/// bình, tổng số, phân bố) KHÔNG đổi theo bộ lọc; số sao ngoài 1–5 trả 400 thay vì im lặng trả danh sách rỗng.</summary>
[Collection("Integration")]
public sealed class ShowRatingsScoreFilterTests
{
    private readonly ApiFactory _factory;
    public ShowRatingsScoreFilterTests(ApiFactory factory) => _factory = factory;

    // Buổi riêng cho lớp test này, 4 đánh giá 5-5-4-1 của 4 người khác nhau (chỉ mục duy nhất UserId + LoungeShowId).
    private async Task<Guid> TaoBuoiCoDanhGia()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var show = new LoungeShow
        {
            LoungeId = SeedHelper.LoungeId,
            Name = $"RatingFilterShow-{Guid.NewGuid():N}",
            Description = "test",
            Format = LoungeShowFormat.Offline,
            Status = LoungeShowStatus.Ended,
            ScheduledStart = DateTimeOffset.UtcNow.AddDays(-5),
            ActualEnd = DateTimeOffset.UtcNow.AddDays(-5).AddHours(3),
        };
        db.LoungeShows.Add(show);
        await db.SaveChangesAsync();
        var nguoi = new[] { SeedHelper.AudienceId, SeedHelper.StaffId, SeedHelper.OwnerId, SeedHelper.AdminId };
        var sao = new[] { 5, 5, 4, 1 };
        for (var i = 0; i < 4; i++)
            db.Add(new LoungeShowRating { UserId = nguoi[i], LoungeShowId = show.Id, Score = sao[i], Comment = $"nhan xet {sao[i]} sao #{i}" });
        await db.SaveChangesAsync();
        return show.Id;
    }

    private static async Task<JsonElement> DocData(HttpResponseMessage res)
        => (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");

    [Fact]
    public async Task Score_LocDanhSach_TongQuanGiuNguyen()
    {
        var showId = await TaoBuoiCoDanhGia();
        var client = _factory.CreateClient();

        var res = await client.GetAsync($"/api/v1/lounge-shows/{showId}/ratings?score=5");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var data = await DocData(res);

        var items = data.GetProperty("items").GetProperty("items").EnumerateArray().ToList();
        items.Should().HaveCount(2);
        items.Should().OnlyContain(x => x.GetProperty("score").GetInt32() == 5);
        data.GetProperty("items").GetProperty("totalCount").GetInt32().Should().Be(2, "tổng của DANH SÁCH theo bộ lọc");

        // Tổng quan tính trên MỌI đánh giá, không theo bộ lọc.
        data.GetProperty("totalCount").GetInt32().Should().Be(4);
        data.GetProperty("averageScore").GetDecimal().Should().Be(3.75m);
        data.GetProperty("scoreDistribution").GetProperty("1").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task KhongCoScore_TraMoiDanhGia()
    {
        var showId = await TaoBuoiCoDanhGia();
        var data = await DocData(await _factory.CreateClient().GetAsync($"/api/v1/lounge-shows/{showId}/ratings"));
        data.GetProperty("items").GetProperty("totalCount").GetInt32().Should().Be(4);
    }

    [Fact]
    public async Task Score_KhongCoDanhGiaNao_TraRong_KhongLoi()
    {
        var showId = await TaoBuoiCoDanhGia();
        var data = await DocData(await _factory.CreateClient().GetAsync($"/api/v1/lounge-shows/{showId}/ratings?score=2"));
        data.GetProperty("items").GetProperty("items").GetArrayLength().Should().Be(0);
        data.GetProperty("totalCount").GetInt32().Should().Be(4);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(-1)]
    public async Task Score_NgoaiKhoang_Tra400(int score)
    {
        var showId = await TaoBuoiCoDanhGia();
        var res = await _factory.CreateClient().GetAsync($"/api/v1/lounge-shows/{showId}/ratings?score={score}");
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await res.Content.ReadAsStringAsync()).Should().Contain("Số sao để lọc phải từ 1 đến 5.");
    }
}
