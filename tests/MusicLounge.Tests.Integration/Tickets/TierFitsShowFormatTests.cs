using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.ValueObjects;
using MusicLounge.Tests.Integration.Helpers;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Tests.Integration.Tickets;

/// <summary>
/// MLACP-509 (fe báo M-432, BE-1). Lúc buổi diễn còn Draft, tạo được hạng vé XEM TRỰC TUYẾN cho buổi Offline — đăng
/// lên là khán giả mua được vé cho buổi không bao giờ phát. Loại hạng vé phải hợp với hình thức ở mọi trạng thái.
/// </summary>
[Collection("Integration")]
public sealed class TierFitsShowFormatTests
{
    private readonly ApiFactory _factory;

    public TierFitsShowFormatTests(ApiFactory factory) => _factory = factory;

    private async Task<(int OwnerId, int ShowId)> BanNhapAsync(LoungeShowFormat hinhThuc)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var lounge = new MusicLoungeEntity
        {
            Owner = new User { Email = $"t509-{Guid.NewGuid():N}@test.com", FullName = "Chu 509" },
            Name = $"Phong {Guid.NewGuid():N}"[..20], Status = LoungeStatus.Approved,
            Address = new VenueAddress { Street = "1 Le Loi", District = "1", City = "HCM" }
        };
        db.Add(lounge);
        await db.SaveChangesAsync();
        var show = new LoungeShow
        {
            LoungeId = lounge.Id, Name = $"Buoi {Guid.NewGuid():N}"[..18], Description = "MLACP-509",
            Format = hinhThuc, Status = LoungeShowStatus.Draft,
            ScheduledStart = DateTimeOffset.UtcNow.AddDays(10), ScheduledEnd = DateTimeOffset.UtcNow.AddDays(10).AddHours(2)
        };
        db.LoungeShows.Add(show);
        await db.SaveChangesAsync();
        return (lounge.OwnerId, show.Id);
    }

    private Task<HttpResponseMessage> TaoHangVeAsync(int ownerId, int showId, string accessType)
        => _factory.CreateAuthenticatedClient(ownerId, "Owner").PostAsJsonAsync("/api/v1/ticket-tiers", new
        {
            ShowId = showId, Name = $"Hang {accessType}", Description = (string?)null, AccessType = accessType,
            ZoneId = (int?)null, TotalCapacity = (int?)null,
            Prices = new[]
            {
                new
                {
                    Name = "Gia", Price = 80_000m, Quota = (int?)null, PurchaseChannel = "Online",
                    SaleStart = DateTimeOffset.UtcNow.AddHours(-1), SaleEnd = (DateTimeOffset?)null
                }
            }
        });

    [Theory]
    [InlineData(LoungeShowFormat.Offline, "Livestream", HttpStatusCode.UnprocessableEntity)]
    [InlineData(LoungeShowFormat.Online, "Physical", HttpStatusCode.UnprocessableEntity)]
    [InlineData(LoungeShowFormat.Online, "Livestream", HttpStatusCode.Created)]
    [InlineData(LoungeShowFormat.Offline, "Physical", HttpStatusCode.Created)]
    [InlineData(LoungeShowFormat.Hybrid, "Livestream", HttpStatusCode.Created)]
    [InlineData(LoungeShowFormat.Hybrid, "Physical", HttpStatusCode.Created)]
    public async Task BanNhap_HangVePhaiHopHinhThuc(LoungeShowFormat hinhThuc, string accessType, HttpStatusCode kyVong)
    {
        var (ownerId, showId) = await BanNhapAsync(hinhThuc);

        var res = await TaoHangVeAsync(ownerId, showId, accessType);

        res.StatusCode.Should().Be(kyVong, await res.Content.ReadAsStringAsync());
    }
}
