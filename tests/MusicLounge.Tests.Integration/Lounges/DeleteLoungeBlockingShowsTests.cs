using System.Net;
using System.Text.Json;
using FluentAssertions;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.ValueObjects;
using MusicLounge.Tests.Integration.Helpers;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Tests.Integration.Lounges;

/// <summary>
/// MLACP-507 (M-405 mục 3). Xoá phòng trà bị 409 "đang có buổi diễn" trong khi thứ chặn là BẢN NHÁP — loại không hiện ở
/// danh sách công khai — nên khi dọn dữ liệu Azure 29/09 phải dò từng id mới biết vì sao. 409 phải nói rõ buổi nào chặn.
/// </summary>
[Collection("Integration")]
public sealed class DeleteLoungeBlockingShowsTests
{
    private readonly ApiFactory _factory;

    public DeleteLoungeBlockingShowsTests(ApiFactory factory) => _factory = factory;

    private static string Ma() => Guid.NewGuid().ToString("N")[..10];

    private async Task<(Guid LoungeId, Guid OwnerId, List<Guid> ShowIds)> PhongTraCoBuoiAsync(params LoungeShowStatus[] trangThai)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var lounge = new MusicLoungeEntity
        {
            Owner = new User { Email = $"d507-{Ma()}@test.com", FullName = "Chu 507" },
            Name = $"Phong {Ma()}", Status = LoungeStatus.Approved,
            Address = new VenueAddress { Street = "1 Le Loi", District = "1", City = "HCM" }
        };
        db.Add(lounge);
        await db.SaveChangesAsync();
        var shows = trangThai.Select(t => new LoungeShow
        {
            LoungeId = lounge.Id, Name = $"Buoi {Ma()}", Description = "test", Format = LoungeShowFormat.Offline, Status = t,
            ScheduledStart = DateTimeOffset.UtcNow.AddDays(10), ScheduledEnd = DateTimeOffset.UtcNow.AddDays(10).AddHours(2)
        }).ToList();
        db.LoungeShows.AddRange(shows);
        await db.SaveChangesAsync();
        return (lounge.Id, lounge.OwnerId, shows.Select(s => s.Id).ToList());
    }

    private static async Task<(HttpStatusCode Status, JsonElement Body)> XoaAsync(HttpClient client, Guid loungeId, string? ngonNgu = null)
    {
        var req = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/lounges/{loungeId}");
        if (ngonNgu is not null) req.Headers.TryAddWithoutValidation("Accept-Language", ngonNgu);
        var res = await client.SendAsync(req);
        var body = await res.Content.ReadAsStringAsync();
        return (res.StatusCode, string.IsNullOrEmpty(body) ? default : JsonDocument.Parse(body).RootElement.Clone());
    }

    [Fact]
    public async Task BiChanBoiBanNhap_409NeuRoTungBuoiVaTrangThai()
    {
        var (loungeId, ownerId, showIds) = await PhongTraCoBuoiAsync(LoungeShowStatus.Draft, LoungeShowStatus.Draft, LoungeShowStatus.Ended);

        var (status, body) = await XoaAsync(_factory.CreateAuthenticatedClient(ownerId, "Owner"), loungeId);

        status.Should().Be(HttpStatusCode.Conflict);
        body.GetProperty("message").GetString().Should().Contain("bản nháp");
        var chan = body.GetProperty("errors").GetProperty("blockingShows").EnumerateArray()
            .Select(x => (x.GetProperty("id").GetGuid(), x.GetProperty("status").GetString())).ToList();
        chan.Should().Equal((showIds[0], "Draft"), (showIds[1], "Draft"), (showIds[2], "Ended"));
    }

    [Fact]
    public async Task AdminCungThayDanhSach_VaCauTiengAnhVanDichDuoc()
    {
        var (loungeId, _, showIds) = await PhongTraCoBuoiAsync(LoungeShowStatus.Draft);
        var admin = _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin");

        var (status, body) = await XoaAsync(admin, loungeId, "en");

        status.Should().Be(HttpStatusCode.Conflict);
        body.GetProperty("message").GetString().Should().StartWith("This lounge still has concerts",
            "câu giữ cố định để còn tra được bản dịch — chi tiết nằm ở errors chứ không nội suy vào câu");
        body.GetProperty("errors").GetProperty("blockingShows")[0].GetProperty("id").GetGuid().Should().Be(showIds[0]);
    }

    [Fact]
    public async Task KhongConBuoiNao_XoaDuocNhuCu()
    {
        var (loungeId, ownerId, _) = await PhongTraCoBuoiAsync();

        var (status, _) = await XoaAsync(_factory.CreateAuthenticatedClient(ownerId, "Owner"), loungeId);

        status.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task NguoiNgoai_403_KhongThayDanhSachBanNhap()
    {
        var (loungeId, _, _) = await PhongTraCoBuoiAsync(LoungeShowStatus.Draft);

        var (status, body) = await XoaAsync(_factory.CreateAuthenticatedClient(SeedHelper.OtherOwnerId, "Owner"), loungeId);

        status.Should().Be(HttpStatusCode.Forbidden, "kiểm quyền chạy TRƯỚC khi liệt kê buổi đang chặn");
        body.TryGetProperty("errors", out var errors).Should().BeTrue();
        errors.ValueKind.Should().Be(JsonValueKind.Null);
    }
}
