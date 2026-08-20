using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Tests.Integration.Helpers;

namespace MusicLounge.Tests.Integration.CF3;

/// <summary>
/// Check-in vé giấy tại cửa — trước session này chưa có test nào (kể cả happy path), phát hiện
/// khi rà lại độ phủ theo role cho FE. Controller CheckIn riêng đã gộp vào TicketsController
/// (trùng logic 100% với endpoint tại đây, chỉ khác route) khi rà soát chuẩn RESTful.
/// POST /api/v1/tickets/check-in
/// </summary>
[Collection("Integration")]
public sealed class CheckInTests
{
    private readonly ApiFactory _factory;

    public CheckInTests(ApiFactory factory) => _factory = factory;

    /// <summary>Seeds an Ongoing offline show with a Confirmed physical ticket ready to scan.</summary>
    private async Task<string> SeedConfirmedPhysicalTicketOnOngoingShowAsync()
    {
        var qrCode = $"QR-{Guid.NewGuid():N}";
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var show = new LoungeShow
        {
            LoungeId = SeedHelper.LoungeId,
            Name = $"CheckInTestShow-{Guid.NewGuid():N}",
            Description = "test",
            Format = LoungeShowFormat.Offline,
            Status = LoungeShowStatus.Ongoing,
            ScheduledStart = DateTimeOffset.UtcNow.AddHours(-1)
        };
        db.LoungeShows.Add(show);
        await db.SaveChangesAsync();

        var tier = new TicketTier
        {
            LoungeShowId = show.Id, Name = "Door", AccessType = AccessType.Physical,
            CreatedAt = DateTime.UtcNow
        };
        db.Add(tier);
        await db.SaveChangesAsync();

        var price = new TicketPrice
        {
            TierId = tier.Id, Name = "Standard", Price = 100_000m,
            PurchaseChannel = PurchaseChannel.Offline,
            SaleStart = DateTimeOffset.UtcNow.AddDays(-1), SaleEnd = DateTimeOffset.UtcNow.AddDays(1)
        };
        db.Add(price);
        await db.SaveChangesAsync();

        db.Add(new Ticket
        {
            Id = Guid.NewGuid(),
            BuyerId = SeedHelper.AudienceId,
            PriceId = price.Id,
            TierId = tier.Id,
            ShowId = show.Id,
            Status = TicketStatus.Confirmed,
            QrCode = qrCode,
            PurchaseChannel = PurchaseChannel.Offline,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        return qrCode;
    }

    [Fact]
    public async Task CheckIn_AsCorrectVenueStaff_MarksTicketUsed()
    {
        var qrCode = await SeedConfirmedPhysicalTicketOnOngoingShowAsync();
        var client = _factory.CreateAuthenticatedClient(SeedHelper.StaffId, "Staff", SeedHelper.LoungeId);

        var res = await client.PostAsJsonAsync("/api/v1/tickets/check-in", new { QrCode = qrCode });

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await res.Content.ReadAsStringAsync();
        body.Should().Contain("\"status\":\"Used\"");
    }

    [Fact]
    public async Task CheckIn_AsAudience_Returns403()
    {
        var qrCode = await SeedConfirmedPhysicalTicketOnOngoingShowAsync();
        var client = _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience");

        var res = await client.PostAsJsonAsync("/api/v1/tickets/check-in", new { QrCode = qrCode });

        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CheckIn_AlreadyCheckedIn_Returns422()
    {
        // Handler flips Status Confirmed→Used on first check-in, so the second attempt fails the
        // "Status != Confirmed" guard (422) before it ever reaches the CheckedInAt/409 guard.
        var qrCode = await SeedConfirmedPhysicalTicketOnOngoingShowAsync();
        var client = _factory.CreateAuthenticatedClient(SeedHelper.StaffId, "Staff", SeedHelper.LoungeId);
        await client.PostAsJsonAsync("/api/v1/tickets/check-in", new { QrCode = qrCode });

        var res = await client.PostAsJsonAsync("/api/v1/tickets/check-in", new { QrCode = qrCode });

        res.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }
}
