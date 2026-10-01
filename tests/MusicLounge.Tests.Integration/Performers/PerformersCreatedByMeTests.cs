using System.Net;
using System.Text.Json;
using FluentAssertions;
using MusicLounge.Domain.Entities;
using MusicLounge.Tests.Integration.Helpers;

namespace MusicLounge.Tests.Integration.Performers;

/// <summary>
/// MLACP-501. Trang Tài khoản nhận tiền cần đúng nghệ sĩ do chủ phòng trà tạo (chỉ người tạo quản lý được tài khoản nhận
/// tiền của nghệ sĩ), nhưng danh mục nghệ sĩ là dùng chung — FE phải lật nhiều trang để tự lọc, quá 500 hồ sơ là thiếu.
/// </summary>
[Collection("Integration")]
public sealed class PerformersCreatedByMeTests
{
    private readonly ApiFactory _factory;

    public PerformersCreatedByMeTests(ApiFactory factory) => _factory = factory;

    // 130 hồ sơ; 3 của chủ phòng trà mới nằm rải ở vị trí 10, 70, 125 — còn lại của chủ phòng trà khác hoặc không rõ người tạo.
    private async Task<(int OwnerId, List<int> CuaToi)> DanhMucCo130HoSoAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var owner = new User { Email = $"p501-{Guid.NewGuid():N}@test.com", FullName = "Chu phong tra 501" };
        db.Users.Add(owner);
        await db.SaveChangesAsync();

        var viTriCuaToi = new HashSet<int> { 10, 70, 125 };
        var hoSo = Enumerable.Range(0, 130).Select(i => new Performer
        {
            Name = $"NgheSi501-{i}-{Guid.NewGuid():N}"[..25],
            CreatedByUserId = viTriCuaToi.Contains(i) ? owner.Id : i % 2 == 0 ? SeedHelper.OwnerId : null
        }).ToList();
        db.AddRange(hoSo);
        await db.SaveChangesAsync();
        return (owner.Id, hoSo.Where(p => p.CreatedByUserId == owner.Id).Select(p => p.Id).ToList());
    }

    private static async Task<(List<int> Ids, int Total)> GoiAsync(HttpClient client, string query)
    {
        var res = await client.GetAsync($"/api/v1/performers?{query}");
        var body = await res.Content.ReadAsStringAsync();
        res.StatusCode.Should().Be(HttpStatusCode.OK, body);
        using var doc = JsonDocument.Parse(body);
        var data = doc.RootElement.GetProperty("data");
        return (data.GetProperty("items").EnumerateArray().Select(x => x.GetProperty("id").GetInt32()).ToList(),
                data.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task CreatedByMe_TraDungBaHoSoCuaNguoiGoi_DuNamRaiRacTrong130()
    {
        var (ownerId, cuaToi) = await DanhMucCo130HoSoAsync();
        var chu = _factory.CreateAuthenticatedClient(ownerId, "Owner");

        var (ids, total) = await GoiAsync(chu, "createdByMe=true&pageSize=50");

        ids.Should().BeEquivalentTo(cuaToi);
        total.Should().Be(3, "totalCount đếm theo người tạo, không phải cả danh mục");
    }

    [Fact]
    public async Task KhongTruyenCreatedByMe_VanLaDanhMucChungNhuCu()
    {
        var (ownerId, _) = await DanhMucCo130HoSoAsync();
        var chu = _factory.CreateAuthenticatedClient(ownerId, "Owner");

        var (_, total) = await GoiAsync(chu, "pageSize=50");

        total.Should().BeGreaterThanOrEqualTo(130, "danh mục dùng chung gồm hồ sơ của mọi người tạo");
    }

    [Fact]
    public async Task ChuaDangNhap_401()
    {
        var res = await _factory.CreateClient().GetAsync("/api/v1/performers?createdByMe=true");

        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
