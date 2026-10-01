using System.Net;
using System.Text.Json;
using FluentAssertions;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Tests.Integration.Helpers;

namespace MusicLounge.Tests.Integration.Moderations;

/// <summary>
/// MLACP-504. Trang chi tiết buổi diễn của Admin tải 100 bản kiểm duyệt đang chờ rồi quét tìm bản của buổi đang mở —
/// quá 100 bản chờ thì hộp duyệt không mở được. Lọc thẳng theo đối tượng phải trả đúng bản đó.
/// </summary>
[Collection("Integration")]
public sealed class PendingModerationByTargetTests
{
    private readonly ApiFactory _factory;

    public PendingModerationByTargetTests(ApiFactory factory) => _factory = factory;

    private HttpClient Admin() => _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin");

    // 120 bản chờ của 120 "buổi diễn" (TargetId không cần là buổi thật — bảng kiểm duyệt không có khoá ngoại tới đối
    // tượng). Dải id riêng mỗi lần chạy để không đụng bản của test khác. Kèm một bản Livestream TRÙNG id với buổi cuối.
    private async Task<(List<int> TargetIds, int BanCuaBuoiCuoi, int BanLivestreamTrungId)> Co120BanChoAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var goc = Random.Shared.Next(1_000_000, 2_000_000_000 - 1_000);
        var targetIds = Enumerable.Range(goc, 120).ToList();
        var ban = targetIds.Select(t => new EventModeration
        {
            TargetType = ModerationTargetType.Show, TargetId = t, AiScore = 0.99f // điểm cao: chen lên đầu hàng chờ
        }).ToList();
        var livestream = new EventModeration { TargetType = ModerationTargetType.Livestream, TargetId = targetIds[^1], AiScore = 0.99f };
        db.AddRange(ban);
        db.Add(livestream);
        await db.SaveChangesAsync();
        return (targetIds, ban[^1].Id, livestream.Id);
    }

    private async Task<List<int>> GoiAsync(string query)
    {
        var res = await Admin().GetAsync($"/api/v1/moderations/pending?{query}");
        var body = await res.Content.ReadAsStringAsync();
        res.StatusCode.Should().Be(HttpStatusCode.OK, body);
        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.GetProperty("data").GetProperty("items").EnumerateArray()
            .Select(x => x.GetProperty("id").GetInt32()).ToList();
    }

    [Fact]
    public async Task LayDungBanCuaBuoiThu120_KhongLanBanLivestreamTrungId()
    {
        var (targetIds, banCuaBuoiCuoi, banLivestream) = await Co120BanChoAsync();

        var ids = await GoiAsync($"targetType=Show&targetId={targetIds[^1]}");

        ids.Should().Equal([banCuaBuoiCuoi], "đúng bản của buổi đó, không lẫn bản Livestream mang cùng số id");
        (await GoiAsync($"targetType=Livestream&targetId={targetIds[^1]}")).Should().Equal([banLivestream]);
    }

    [Fact]
    public async Task TargetIdThieuTargetType_400_ChuKhongTraLanCacLoai()
    {
        var res = await Admin().GetAsync("/api/v1/moderations/pending?targetId=5");

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest, await res.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task KhongTruyenTargetId_VanLaHangChoNhuCu()
    {
        await Co120BanChoAsync();

        var ids = await GoiAsync("targetType=Show&pageSize=100");

        ids.Should().HaveCount(100, "không lọc đối tượng thì vẫn là cả hàng chờ, phân trang như trước");
    }
}
