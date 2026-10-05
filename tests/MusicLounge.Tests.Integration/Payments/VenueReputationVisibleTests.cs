using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.ValueObjects;
using MusicLounge.Infrastructure.Persistence;
using MusicLounge.Tests.Integration.Helpers;
using MusicLoungeVenue = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Tests.Integration.Payments;

/// <summary>
/// MLACP-671. Điểm uy tín quyết định phần tiền vé chuyển trước cho phòng trà (Mới 50% / Tiêu chuẩn 70% / Cao cấp 80%),
/// nhưng trước đây chỉ tồn tại bên trong lúc xếp lịch quyết toán: chủ phòng trà không thấy mình hạng nào, và cột đệm
/// ReputationScore chỉ đổi khi có vé bán — đánh giá mới hay bị gỡ không làm bảng xếp hạng của Admin đổi.
/// </summary>
[Collection("Integration")]
public sealed class VenueReputationVisibleTests
{
    private readonly ApiFactory _factory;

    public VenueReputationVisibleTests(ApiFactory factory) => _factory = factory;

    private sealed record Venue(Guid OwnerId, Guid LoungeId, string Name, Guid ShowId);

    private async Task<Venue> VenueWithRatingsAsync(params int[] stars)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var owner = new User
        {
            Email = $"rep671-{Guid.NewGuid():N}@test.com", FullName = "Chủ Uy Tín", Role = UserRole.Owner,
            IsActive = true, EmailVerifiedAt = DateTimeOffset.UtcNow
        };
        db.Users.Add(owner);
        await db.SaveChangesAsync();
        var name = $"Rep671 {Guid.NewGuid():N}"[..20];
        var lounge = new MusicLoungeVenue
        {
            OwnerId = owner.Id, Name = name, Status = LoungeStatus.Approved,
            Address = new VenueAddress { Street = "1 Lê Lợi", District = "1", City = "HCM" }
        };
        db.Add(lounge);
        await db.SaveChangesAsync();
        var show = new LoungeShow
        {
            LoungeId = lounge.Id, Name = $"Rep671Show-{Guid.NewGuid():N}", Description = "test",
            Format = LoungeShowFormat.Offline, Status = LoungeShowStatus.Ended,
            ScheduledStart = DateTimeOffset.UtcNow.AddDays(-5), ActualEnd = DateTimeOffset.UtcNow.AddDays(-5).AddHours(3),
        };
        db.LoungeShows.Add(show);
        await db.SaveChangesAsync();
        var raters = new[] { SeedHelper.AudienceId, SeedHelper.StaffId, SeedHelper.AdminId, SeedHelper.OwnerId };
        for (var i = 0; i < stars.Length; i++)
            db.Add(new LoungeShowRating { UserId = raters[i], LoungeShowId = show.Id, Score = stars[i] });
        await db.SaveChangesAsync();
        return new Venue(owner.Id, lounge.Id, name, show.Id);
    }

    private async Task<JsonElement> StandingAsSeenByOwnerAsync(Venue v)
    {
        var res = await _factory.CreateAuthenticatedClient(v.OwnerId, "Owner").GetAsync("/api/v1/me/earnings");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var data = JsonDocument.Parse(await res.Content.ReadAsStringAsync()).RootElement.GetProperty("data");
        data.GetProperty("loungeNames").GetProperty(v.LoungeId.ToString()).GetString().Should().Be(v.Name);
        return data.GetProperty("standings").EnumerateArray().Single(s => s.GetProperty("loungeId").GetGuid() == v.LoungeId);
    }

    [Fact]
    public void Owner_SeesTheirTier_AndTheShareItPays()
    {
        var v = VenueWithRatingsAsync(5, 4, 3).GetAwaiter().GetResult();

        var s = StandingAsSeenByOwnerAsync(v).GetAwaiter().GetResult();

        s.GetProperty("score").GetDecimal().Should().Be(4m);
        s.GetProperty("ratingCount").GetInt32().Should().Be(3);
        s.GetProperty("tier").GetString().Should().Be("Standard", "trung bình 4 ≥ ngưỡng 3.5");
        s.GetProperty("preRate").GetDecimal().Should().Be(0.70m);
        s.GetProperty("rules").GetProperty("premiumMinShows").GetInt32().Should().Be(10,
            "màn hình cần ngưỡng để nói 'cần thêm gì để lên hạng' mà không viết cứng con số");
    }

    [Fact]
    public async Task ANewVenue_IsInTheNewTier()
    {
        var v = await VenueWithRatingsAsync();

        var s = await StandingAsSeenByOwnerAsync(v);

        s.GetProperty("tier").GetString().Should().Be("New");
        s.GetProperty("preRate").GetDecimal().Should().Be(0.50m);
        s.GetProperty("ratingCount").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task RemovingARating_ChangesTheStanding_WithoutWaitingForATicketSale()
    {
        var v = await VenueWithRatingsAsync(5, 4, 1);
        (await StandingAsSeenByOwnerAsync(v)).GetProperty("tier").GetString().Should().Be("New", "trung bình 3.33 < 3.5");

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var low = await db.Set<LoungeShowRating>().SingleAsync(r => r.LoungeShowId == v.ShowId && r.Score == 1);
            low.IsRemoved = true;
            await db.SaveChangesAsync();
        }

        var after = await StandingAsSeenByOwnerAsync(v);
        after.GetProperty("score").GetDecimal().Should().Be(4.5m);
        after.GetProperty("tier").GetString().Should().Be("Standard",
            "đánh giá bị gỡ phải được tính lại ngay — trước đây điểm chỉ đổi khi có vé bán");
    }

    // Bảng xếp hạng của Admin (GET /analytics/admin-content-overview) dùng cùng VenueReputation nhưng KHÔNG kiểm được ở
    // đây: cùng handler có truy vấn VenuePenalty.IssuedAt (DateTimeOffset) mà SQLite không dịch được → 500 trong môi
    // trường test, có từ trước task này. Kiểm trên Azure (SQL Server) sau khi deploy.
}
