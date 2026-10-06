using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.ValueObjects;
using MusicLounge.Infrastructure.Persistence;
using MusicLounge.Tests.Integration.Helpers;
using MusicLoungeVenue = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Tests.Integration.Search;

/// <summary>
/// MLACP-682. Ô tìm kiếm chung trước đây chỉ gợi ý buổi diễn. Nay gợi ý cả nghệ sĩ — nhưng nghệ sĩ không có tài khoản, hồ
/// sơ do phòng trà tạo, nên chỉ những người đã xuất hiện ở buổi diễn công khai của phòng trà đang hoạt động mới được đem ra
/// trước người lạ. Hồ sơ chỉ nằm trong bản nháp, hay chỉ diễn ở phòng trà đã bị đình chỉ, không được lộ.
/// </summary>
[Collection("Integration")]
public sealed class PerformerSuggestionsTests
{
    private readonly ApiFactory _factory;

    public PerformerSuggestionsTests(ApiFactory factory) => _factory = factory;

    private sealed record Item(Guid Id, string Name, string? AvatarUrl);
    private sealed record Envelope(bool Success, List<Item> Data);

    private async Task<Guid> PerformerAsync(string name, LoungeStatus venueStatus, LoungeShowStatus showStatus)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var owner = new User { Email = $"ps-{Guid.NewGuid():N}@test.com", FullName = "Chu phong tra" };
        db.Users.Add(owner);
        await db.SaveChangesAsync();
        var lounge = new MusicLoungeVenue
        {
            OwnerId = owner.Id, Name = $"Ps-{Guid.NewGuid():N}"[..20], Status = venueStatus,
            Address = new VenueAddress { Street = "1 Test St", District = "1", City = "HCM" }
        };
        db.Lounges.Add(lounge);
        await db.SaveChangesAsync();
        var start = DateTimeOffset.UtcNow.AddDays(3);
        var show = new LoungeShow
        {
            LoungeId = lounge.Id, Name = $"PsShow-{Guid.NewGuid():N}", Description = "test",
            Format = LoungeShowFormat.Offline, Status = showStatus, ScheduledStart = start, ScheduledEnd = start.AddHours(2)
        };
        var performer = new Performer { Name = name, CreatedByUserId = owner.Id };
        db.Add(show);
        db.Add(performer);
        await db.SaveChangesAsync();
        db.Add(new Performance { LoungeShowId = show.Id, PerformerId = performer.Id });
        await db.SaveChangesAsync();
        return performer.Id;
    }

    private async Task<List<Item>> GoiYAsync(string q)
    {
        var res = await _factory.CreateClient().GetAsync($"/api/v1/performers/suggestions?q={Uri.EscapeDataString(q)}");
        res.StatusCode.Should().Be(HttpStatusCode.OK, "ô tìm kiếm công khai — khách chưa đăng nhập cũng dùng");
        return (await res.Content.ReadFromJsonAsync<Envelope>())!.Data;
    }

    [Fact]
    public async Task Khach_ThayNgheSiCoBuoiCongKhai_KhongThayHoSoChuaCongBo()
    {
        var ma = Guid.NewGuid().ToString("N")[..8];
        var congKhai = await PerformerAsync($"Lan Anh {ma} A", LoungeStatus.Approved, LoungeShowStatus.Published);
        var daDien = await PerformerAsync($"Lan Anh {ma} B", LoungeStatus.Warned, LoungeShowStatus.Ended);
        var banNhap = await PerformerAsync($"Lan Anh {ma} C", LoungeStatus.Approved, LoungeShowStatus.Draft);
        var choDuyet = await PerformerAsync($"Lan Anh {ma} D", LoungeStatus.Approved, LoungeShowStatus.Pending);
        var dinhChi = await PerformerAsync($"Lan Anh {ma} E", LoungeStatus.Suspended, LoungeShowStatus.Published);

        var ids = (await GoiYAsync($"LAN ANH {ma}")).Select(x => x.Id).ToList();

        ids.Should().BeEquivalentTo([congKhai, daDien], "chỉ nghệ sĩ đã diễn công khai ở phòng trà đang hoạt động");
        ids.Should().NotContain([banNhap, choDuyet, dinhChi]);
    }

    [Fact]
    public async Task TuKhoaRong_TraDanhSachRong()
        => (await GoiYAsync("  ")).Should().BeEmpty();
}
