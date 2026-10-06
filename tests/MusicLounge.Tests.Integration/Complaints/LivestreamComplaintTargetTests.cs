using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.ValueObjects;
using MusicLounge.Infrastructure.Persistence;
using MusicLounge.Tests.Integration.Helpers;
using MusicLoungeVenue = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Tests.Integration.Complaints;

/// <summary>
/// MLACP-680. Trang xem trực tuyến là /livestream/&lt;mã BUỔI DIỄN&gt;, nên người dán đường dẫn trang đó vào khiếu nại loại
/// "livestream" mang mã buổi diễn chứ không phải Livestream.Id — trước đây bị trả 400 "không tồn tại". Nay nhận, và lưu
/// về Livestream.Id để TargetId của loại này chỉ có một nghĩa (ReferenceNames, hàng đợi Admin đọc theo Livestream.Id).
/// </summary>
[Collection("Integration")]
public sealed class LivestreamComplaintTargetTests
{
    private readonly ApiFactory _factory;

    public LivestreamComplaintTargetTests(ApiFactory factory) => _factory = factory;

    private async Task<(Guid ShowId, Guid LivestreamId)> SeedAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var owner = new User { Email = $"ls-cmp-{Guid.NewGuid():N}@test.com", FullName = "Chu phong tra" };
        db.Users.Add(owner);
        await db.SaveChangesAsync();

        var lounge = new MusicLoungeVenue
        {
            OwnerId = owner.Id, Name = $"LsCmp-{Guid.NewGuid():N}"[..20], Status = LoungeStatus.Approved,
            Address = new VenueAddress { Street = "1 Test St", District = "1", City = "HCM" }
        };
        db.Lounges.Add(lounge);
        await db.SaveChangesAsync();

        var start = DateTimeOffset.UtcNow.AddHours(-1);
        var show = new LoungeShow
        {
            LoungeId = lounge.Id, Name = $"LsCmpShow-{Guid.NewGuid():N}", Description = "test",
            Format = LoungeShowFormat.Online, Status = LoungeShowStatus.Ongoing,
            ScheduledStart = start, ScheduledEnd = start.AddHours(3)
        };
        db.Add(show);
        await db.SaveChangesAsync();

        var livestream = new Livestream { LoungeShowId = show.Id, Status = LivestreamStatus.Live, StartedAt = start };
        db.Add(livestream);
        await db.SaveChangesAsync();
        return (show.Id, livestream.Id);
    }

    private async Task<(HttpStatusCode Status, Complaint? Saved)> KhieuNaiAsync(Guid targetId)
    {
        var moTa = $"Buổi phát có nội dung phản cảm {Guid.NewGuid():N}";
        var res = await _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience").PostAsJsonAsync(
            "/api/v1/complaints", new
            {
                TargetType = "livestream", TargetId = targetId, Category = "Other",
                Description = moTa, EvidenceUrls = (string?)null, ContactPhone = (string?)null
            });

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var saved = await db.Set<Complaint>().AsNoTracking().FirstOrDefaultAsync(c => c.Description == moTa);
        return (res.StatusCode, saved);
    }

    [Fact]
    public async Task MaBuoiDienTuDuongDanTrangXem_DuocNhan_VaLuuVeMaLivestream()
    {
        var (showId, livestreamId) = await SeedAsync();

        var (status, saved) = await KhieuNaiAsync(showId);

        status.Should().Be(HttpStatusCode.Created);
        saved!.TargetId.Should().Be(livestreamId);
    }

    [Fact]
    public async Task MaLivestream_VanDuocNhan_NhuCu()
    {
        var (_, livestreamId) = await SeedAsync();

        var (status, saved) = await KhieuNaiAsync(livestreamId);

        status.Should().Be(HttpStatusCode.Created);
        saved!.TargetId.Should().Be(livestreamId);
    }

    [Fact]
    public async Task MaKhongTroToiDau_BiTuChoi()
        => (await KhieuNaiAsync(Guid.NewGuid())).Status.Should().Be(HttpStatusCode.BadRequest);
}
