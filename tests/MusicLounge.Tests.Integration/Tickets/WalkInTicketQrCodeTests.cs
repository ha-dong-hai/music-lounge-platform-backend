using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Tests.Integration.Helpers;

namespace MusicLounge.Tests.Integration.Tickets;

/// <summary>
/// MLACP-402. Vé bán tại quầy không có người mua, và trước task này không API nào trả mã QR của nó: kết quả bán chỉ có
/// TicketIds, chi tiết vé chỉ người mua xem được, danh sách đơn của buổi diễn không có QR. Vé đã thu tiền mặt nhưng không
/// bao giờ quét được ở cửa. CheckInTests không bắt được vì các test đó ghi thẳng mã QR vào DB — ở đây mã QR phải đi qua
/// đúng đường nhân viên dùng.
/// </summary>
[Collection("Integration")]
public sealed class WalkInTicketQrCodeTests
{
    private readonly ApiFactory _factory;

    public WalkInTicketQrCodeTests(ApiFactory factory) => _factory = factory;

    private HttpClient VenueStaff() =>
        _factory.CreateAuthenticatedClient(SeedHelper.StaffId, "Staff", SeedHelper.LoungeId);

    /// <summary>Buổi diễn tại chỗ đang diễn ở phòng trà seed, có một đợt bán còn mở tại quầy. Tạo riêng cho từng test để
    /// không đổi trạng thái của các buổi diễn seed dùng chung.</summary>
    private async Task<(int TierId, int PriceId)> SeedOngoingShowWithCounterPriceAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var show = new LoungeShow
        {
            LoungeId = SeedHelper.LoungeId,
            Name = $"WalkInQr-{Guid.NewGuid():N}",
            Description = "test",
            Format = LoungeShowFormat.Offline,
            Status = LoungeShowStatus.Ongoing,
            ScheduledStart = DateTimeOffset.UtcNow.AddHours(-1),
            ScheduledEnd = DateTimeOffset.UtcNow.AddHours(3)
        };
        db.LoungeShows.Add(show);
        await db.SaveChangesAsync();

        var tier = new TicketTier
        {
            LoungeShowId = show.Id, Name = "Door", AccessType = AccessType.Physical, TotalCapacity = 20,
            CreatedAt = DateTime.UtcNow
        };
        db.Add(tier);
        await db.SaveChangesAsync();

        var price = new TicketPrice
        {
            TierId = tier.Id, Name = "Standard", Price = 150_000m, Quota = 20, PurchaseChannel = PurchaseChannel.Both,
            SaleStart = DateTimeOffset.UtcNow.AddDays(-1), SaleEnd = DateTimeOffset.UtcNow.AddDays(1)
        };
        db.Add(price);
        await db.SaveChangesAsync();

        return (tier.Id, price.Id);
    }

    private async Task<List<(Guid TicketId, string QrCode)>> SellAtCounterAsync(int priceId, int quantity)
    {
        var res = await VenueStaff().PostAsJsonAsync("/api/v1/tickets/walk-in", new { PriceId = priceId, Quantity = quantity });
        res.StatusCode.Should().Be(HttpStatusCode.Created);

        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("data").GetProperty("tickets").EnumerateArray()
            .Select(t => (t.GetProperty("ticketId").GetGuid(), t.GetProperty("qrCode").GetString()!))
            .ToList();
    }

    [Fact]
    public async Task SellingAtTheCounter_ReturnsTheQrCodeOfEveryTicketSold()
    {
        var (_, priceId) = await SeedOngoingShowWithCounterPriceAsync();

        var sold = await SellAtCounterAsync(priceId, 2);

        sold.Should().HaveCount(2);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        foreach (var (ticketId, qrCode) in sold)
        {
            qrCode.Should().NotBeNullOrWhiteSpace();
            (await db.Tickets.AsNoTracking().SingleAsync(t => t.Id == ticketId)).QrCode.Should().Be(qrCode);
        }
    }

    [Fact]
    public async Task TheQrCodeFromTheSale_ChecksTheGuestIn()
    {
        var (_, priceId) = await SeedOngoingShowWithCounterPriceAsync();
        var sold = await SellAtCounterAsync(priceId, 1);

        var res = await VenueStaff().PostAsJsonAsync("/api/v1/tickets/check-in", new { QrCode = sold[0].QrCode });

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        (await res.Content.ReadAsStringAsync()).Should().Contain("\"status\":\"Used\"");
    }

    [Fact]
    public async Task VenueStaff_CanReopenACounterTicket_ToReprintIt()
    {
        var (_, priceId) = await SeedOngoingShowWithCounterPriceAsync();
        var sold = await SellAtCounterAsync(priceId, 1);

        var res = await VenueStaff().GetAsync($"/api/v1/tickets/{sold[0].TicketId}");

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        (await res.Content.ReadAsStringAsync()).Should().Contain($"\"qrCode\":\"{sold[0].QrCode}\"");
    }

    [Fact]
    public async Task TheVenueOwner_CanReopenACounterTicket()
    {
        var (_, priceId) = await SeedOngoingShowWithCounterPriceAsync();
        var sold = await SellAtCounterAsync(priceId, 1);
        var owner = _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner");

        var res = await owner.GetAsync($"/api/v1/tickets/{sold[0].TicketId}");

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        (await res.Content.ReadAsStringAsync()).Should().Contain($"\"qrCode\":\"{sold[0].QrCode}\"");
    }

    [Fact]
    public async Task StaffOfAnotherVenue_CannotOpenACounterTicket()
    {
        var (_, priceId) = await SeedOngoingShowWithCounterPriceAsync();
        var sold = await SellAtCounterAsync(priceId, 1);
        var otherVenueStaff = _factory.CreateAuthenticatedClient(SeedHelper.OtherVenueStaffId, "Staff", SeedHelper.OtherLoungeId);

        var res = await otherVenueStaff.GetAsync($"/api/v1/tickets/{sold[0].TicketId}");

        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── MLACP-410: quầy mất phản hồi rồi bấm bán lại không được thành bán trùng ──────────────────────────────

    private static async Task<(int PaymentId, List<string> QrCodes)> ReadSaleAsync(HttpResponseMessage res)
    {
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        var data = doc.RootElement.GetProperty("data");
        return (data.GetProperty("paymentId").GetInt32(),
            data.GetProperty("tickets").EnumerateArray().Select(t => t.GetProperty("qrCode").GetString()!).ToList());
    }

    private async Task<int> TicketsSoldForPriceAsync(int priceId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Tickets.AsNoTracking().CountAsync(t => t.PriceId == priceId);
    }

    [Fact]
    public async Task ResendingTheSameSale_ReturnsTheFirstSale_AndSellsNothingMore()
    {
        var (_, priceId) = await SeedOngoingShowWithCounterPriceAsync();
        var sale = new { PriceId = priceId, Quantity = 2, ClientRequestId = Guid.NewGuid() };

        var first = await VenueStaff().PostAsJsonAsync("/api/v1/tickets/walk-in", sale);
        var retry = await VenueStaff().PostAsJsonAsync("/api/v1/tickets/walk-in", sale);

        first.StatusCode.Should().Be(HttpStatusCode.Created);
        retry.StatusCode.Should().Be(HttpStatusCode.Created);
        var (firstPayment, firstQrs) = await ReadSaleAsync(first);
        var (retryPayment, retryQrs) = await ReadSaleAsync(retry);
        retryPayment.Should().Be(firstPayment);
        retryQrs.Should().BeEquivalentTo(firstQrs);
        (await TicketsSoldForPriceAsync(priceId)).Should().Be(2);
    }

    [Fact]
    public async Task ReusingARequestIdForADifferentSale_Returns409()
    {
        var (_, priceId) = await SeedOngoingShowWithCounterPriceAsync();
        var requestId = Guid.NewGuid();
        (await VenueStaff().PostAsJsonAsync("/api/v1/tickets/walk-in",
            new { PriceId = priceId, Quantity = 1, ClientRequestId = requestId })).StatusCode.Should().Be(HttpStatusCode.Created);

        var res = await VenueStaff().PostAsJsonAsync("/api/v1/tickets/walk-in",
            new { PriceId = priceId, Quantity = 3, ClientRequestId = requestId });

        res.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await TicketsSoldForPriceAsync(priceId)).Should().Be(1);
    }

    [Fact]
    public async Task TheSameRequestIdFromAnotherCounterStaff_IsASeparateSale()
    {
        // Ma do may quay sinh; khoa theo tung nhan vien de hai may khong the de len luot ban cua nhau.
        var (_, priceId) = await SeedOngoingShowWithCounterPriceAsync();
        var requestId = Guid.NewGuid();
        var owner = _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner");

        var byStaff = await VenueStaff().PostAsJsonAsync("/api/v1/tickets/walk-in",
            new { PriceId = priceId, Quantity = 1, ClientRequestId = requestId });
        var byOwner = await owner.PostAsJsonAsync("/api/v1/tickets/walk-in",
            new { PriceId = priceId, Quantity = 1, ClientRequestId = requestId });

        byStaff.StatusCode.Should().Be(HttpStatusCode.Created);
        byOwner.StatusCode.Should().Be(HttpStatusCode.Created);
        (await ReadSaleAsync(byOwner)).PaymentId.Should().NotBe((await ReadSaleAsync(byStaff)).PaymentId);
        (await TicketsSoldForPriceAsync(priceId)).Should().Be(2);
    }

    [Fact]
    public async Task WithoutARequestId_TwoSalesAreStillTwoSales()
    {
        var (_, priceId) = await SeedOngoingShowWithCounterPriceAsync();

        await SellAtCounterAsync(priceId, 1);
        await SellAtCounterAsync(priceId, 1);

        (await TicketsSoldForPriceAsync(priceId)).Should().Be(2);
    }

    [Fact]
    public async Task VenueStaff_StillCannotOpenATicketBoughtByAnAudienceMember()
    {
        var (tierId, priceId) = await SeedOngoingShowWithCounterPriceAsync();
        var ticketId = Guid.NewGuid();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var price = await db.Set<TicketPrice>().SingleAsync(p => p.Id == priceId);
            var tier = await db.Set<TicketTier>().SingleAsync(t => t.Id == tierId);
            db.Add(new Ticket
            {
                Id = ticketId,
                BuyerId = SeedHelper.AudienceId,
                PriceId = price.Id,
                TierId = tier.Id,
                ShowId = tier.LoungeShowId,
                Status = TicketStatus.Confirmed,
                QrCode = $"QR-{Guid.NewGuid():N}",
                PurchaseChannel = PurchaseChannel.Online,
                CreatedAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();
        }

        var asStaff = await VenueStaff().GetAsync($"/api/v1/tickets/{ticketId}");
        var asBuyer = await _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience").GetAsync($"/api/v1/tickets/{ticketId}");

        asStaff.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        asBuyer.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
