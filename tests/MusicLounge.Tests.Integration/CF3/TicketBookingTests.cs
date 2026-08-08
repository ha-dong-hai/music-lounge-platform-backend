using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Infrastructure.Persistence;
using MusicLounge.Tests.Integration.Helpers;

namespace MusicLounge.Tests.Integration.CF3;

/// <summary>
/// CF3 W14/W26 — Walk-in ticket sale + ticket cancellation/refund request
/// POST /api/v1/tickets/walk-in
/// POST /api/v1/tickets/{id}/cancel
/// </summary>
[Collection("Integration")]
public sealed class TicketBookingTests
{
    private readonly ApiFactory _factory;

    public TicketBookingTests(ApiFactory factory) => _factory = factory;

    private async Task<int> CreatePhysicalPriceAsync(
        int showId, bool onlineOnly = false, int quota = 50)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var tier = new TicketTier
        {
            LoungeShowId = showId,
            Name = "Door Sale",
            AccessType = AccessType.Physical,
            TotalCapacity = quota,
            CreatedAt = DateTime.UtcNow
        };
        db.Add(tier);
        await db.SaveChangesAsync();

        var price = new TicketPrice
        {
            TierId = tier.Id,
            Name = "Standard",
            Price = 100_000m,
            Quota = quota,
            PurchaseChannel = onlineOnly ? PurchaseChannel.Online : PurchaseChannel.Offline,
            SaleStart = DateTimeOffset.UtcNow.AddDays(-1),
            SaleEnd = DateTimeOffset.UtcNow.AddDays(2)
        };
        db.Add(price);
        await db.SaveChangesAsync();

        return price.Id;
    }

    // ─── W14 Walk-in sale ─────────────────────────────────────────────────────

    [Fact]
    public async Task SellWalkIn_AsStaff_Returns200()
    {
        var priceId = await CreatePhysicalPriceAsync(SeedHelper.OfflineShowId);
        var client = _factory.CreateAuthenticatedClient(SeedHelper.StaffId, "Staff", SeedHelper.LoungeId);

        var res = await client.PostAsJsonAsync("/api/v1/tickets/walk-in", new { PriceId = priceId, Quantity = 2 });

        res.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await res.Content.ReadAsStringAsync();
        body.Should().Contain("\"amount\":200000");
    }

    [Fact]
    public async Task SellWalkIn_AsAudience_Returns403()
    {
        var priceId = await CreatePhysicalPriceAsync(SeedHelper.OfflineShowId);
        var client = _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience");

        var res = await client.PostAsJsonAsync("/api/v1/tickets/walk-in", new { PriceId = priceId, Quantity = 1 });

        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task SellWalkIn_OnlineOnlyChannel_Returns422()
    {
        var priceId = await CreatePhysicalPriceAsync(SeedHelper.OfflineShowId, onlineOnly: true);
        var client = _factory.CreateAuthenticatedClient(SeedHelper.StaffId, "Staff", SeedHelper.LoungeId);

        var res = await client.PostAsJsonAsync("/api/v1/tickets/walk-in", new { PriceId = priceId, Quantity = 1 });

        res.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task SellWalkIn_WrongVenueStaff_Returns403()
    {
        var priceId = await CreatePhysicalPriceAsync(SeedHelper.OfflineShowId);
        // Staff assigned to a different lounge (id 999) than the show's venue (SeedHelper.LoungeId)
        var client = _factory.CreateAuthenticatedClient(SeedHelper.StaffId, "Staff", 999);

        var res = await client.PostAsJsonAsync("/api/v1/tickets/walk-in", new { PriceId = priceId, Quantity = 1 });

        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// Regression test for the check-then-act oversell race: without IShowBookingLock serializing
    /// quota validation per show, N concurrent requests against a 1-seat price can all read
    /// "1 available" before any of them commits, and all succeed. Fires 8 concurrent walk-in
    /// sales at a price with Quota=1 and asserts exactly 1 wins.
    /// </summary>
    [Fact]
    public async Task SellWalkIn_ConcurrentRequestsAgainstLastSeat_OnlyOneSucceeds()
    {
        var priceId = await CreatePhysicalPriceAsync(SeedHelper.OfflineShowId, quota: 1);
        var client = _factory.CreateAuthenticatedClient(SeedHelper.StaffId, "Staff", SeedHelper.LoungeId);

        var responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ =>
            client.PostAsJsonAsync("/api/v1/tickets/walk-in", new { PriceId = priceId, Quantity = 1 })));

        responses.Count(r => r.StatusCode == HttpStatusCode.Created).Should().Be(1,
            "chỉ còn 1 vé — dù 8 request bắn đồng thời, chỉ đúng 1 request được thành công");
        responses.Count(r => r.StatusCode == HttpStatusCode.UnprocessableEntity).Should().Be(7,
            "7 request còn lại phải thấy đúng lỗi hết vé, không phải lỗi hạ tầng (500)");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var confirmedCount = await db.Tickets.CountAsync(
            t => t.PriceId == priceId && t.Status == TicketStatus.Confirmed);
        confirmedCount.Should().Be(1, "không được bán vượt quota dù có tranh chấp đồng thời");
    }

    // ─── W26 Cancel ticket ────────────────────────────────────────────────────

    /// <summary>Creates a Confirmed ticket (with an attached Payment) for AudienceId on a fresh show.</summary>
    private async Task<Guid> CreateConfirmedTicketWithPaymentAsync(
        bool cancellationAllowed = true, decimal? refundPercentage = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var show = new MusicLounge.Domain.Entities.LoungeShow
        {
            LoungeId = SeedHelper.LoungeId,
            Name = $"CancelTestShow-{Guid.NewGuid():N}",
            Description = "Integration test show",
            Format = LoungeShowFormat.Offline,
            Status = LoungeShowStatus.Published,
            ScheduledStart = DateTimeOffset.UtcNow.AddDays(5),
            CancellationAllowed = cancellationAllowed,
            RefundPercentage = refundPercentage
        };
        db.LoungeShows.Add(show);
        await db.SaveChangesAsync();

        var tier = new TicketTier
        {
            LoungeShowId = show.Id, Name = "Standard", AccessType = AccessType.Physical,
            CreatedAt = DateTime.UtcNow
        };
        db.Add(tier);
        await db.SaveChangesAsync();

        var price = new TicketPrice
        {
            TierId = tier.Id, Name = "Standard", Price = 100_000m,
            PurchaseChannel = PurchaseChannel.Both,
            SaleStart = DateTimeOffset.UtcNow.AddDays(-1), SaleEnd = DateTimeOffset.UtcNow.AddDays(4)
        };
        db.Add(price);
        await db.SaveChangesAsync();

        var payment = new Payment
        {
            OrderId = $"T-{Guid.NewGuid():N}"[..30],
            GrossAmount = 100_000m,
            Status = PaymentStatus.Confirmed,
            ReferenceType = "TicketHold",
            ReferenceId = "0",
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.Add(payment);
        await db.SaveChangesAsync();

        var ticket = new Ticket
        {
            Id = Guid.NewGuid(),
            BuyerId = SeedHelper.AudienceId,
            PriceId = price.Id,
            TierId = tier.Id,
            ShowId = show.Id,
            PaymentId = payment.Id,
            Status = TicketStatus.Confirmed,
            PurchaseChannel = PurchaseChannel.Online,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.Add(ticket);
        await db.SaveChangesAsync();

        return ticket.Id;
    }

    [Fact]
    public async Task CancelTicket_ByBuyer_Returns200WithRefundRequest()
    {
        var ticketId = await CreateConfirmedTicketWithPaymentAsync();
        var client = _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience");

        var res = await client.PostAsync($"/api/v1/tickets/{ticketId}/cancel", null);

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await res.Content.ReadAsStringAsync();
        body.Should().Contain("\"success\":true");
    }

    [Fact]
    public async Task CancelTicket_ByNonBuyer_Returns403()
    {
        var ticketId = await CreateConfirmedTicketWithPaymentAsync();
        var client = _factory.CreateAuthenticatedClient(SeedHelper.OtherOwnerId, "Owner");

        var res = await client.PostAsync($"/api/v1/tickets/{ticketId}/cancel", null);

        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CancelTicket_WhenCancellationNotAllowed_Returns422()
    {
        var ticketId = await CreateConfirmedTicketWithPaymentAsync(cancellationAllowed: false);
        var client = _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience");

        var res = await client.PostAsync($"/api/v1/tickets/{ticketId}/cancel", null);

        res.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    private async Task<Guid> CreatePendingTicketWithPaymentAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var show = new MusicLounge.Domain.Entities.LoungeShow
        {
            LoungeId = SeedHelper.LoungeId,
            Name = $"PendingCancelTestShow-{Guid.NewGuid():N}",
            Description = "Integration test show",
            Format = LoungeShowFormat.Offline,
            Status = LoungeShowStatus.Published,
            ScheduledStart = DateTimeOffset.UtcNow.AddDays(5)
        };
        db.LoungeShows.Add(show);
        await db.SaveChangesAsync();

        var tier = new TicketTier
        {
            LoungeShowId = show.Id, Name = "Standard", AccessType = AccessType.Physical,
            CreatedAt = DateTime.UtcNow
        };
        db.Add(tier);
        await db.SaveChangesAsync();

        var price = new TicketPrice
        {
            TierId = tier.Id, Name = "Standard", Price = 100_000m,
            PurchaseChannel = PurchaseChannel.Both,
            SaleStart = DateTimeOffset.UtcNow.AddDays(-1), SaleEnd = DateTimeOffset.UtcNow.AddDays(4)
        };
        db.Add(price);
        await db.SaveChangesAsync();

        var payment = new Payment
        {
            OrderId = $"T-{Guid.NewGuid():N}"[..30],
            GrossAmount = 100_000m,
            Status = PaymentStatus.Pending,
            ReferenceType = "TicketHold",
            ReferenceId = "0",
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.Add(payment);
        await db.SaveChangesAsync();

        var ticket = new Ticket
        {
            Id = Guid.NewGuid(),
            BuyerId = SeedHelper.AudienceId,
            PriceId = price.Id,
            TierId = tier.Id,
            ShowId = show.Id,
            PaymentId = payment.Id,
            Status = TicketStatus.Pending,
            PurchaseChannel = PurchaseChannel.Online,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.Add(ticket);
        await db.SaveChangesAsync();

        return ticket.Id;
    }

    [Fact]
    public async Task CancelTicket_PendingUnpaidTicket_ByBuyer_CancelsImmediatelyWithoutRefundRequest()
    {
        var ticketId = await CreatePendingTicketWithPaymentAsync();
        var client = _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience");

        var res = await client.PostAsync($"/api/v1/tickets/{ticketId}/cancel", null);

        res.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var ticket = await db.Tickets.SingleAsync(t => t.Id == ticketId);
        ticket.Status.Should().Be(TicketStatus.Cancelled);

        var payment = await db.Payments.SingleAsync(p => p.Id == ticket.PaymentId);
        payment.Status.Should().Be(PaymentStatus.Failed);

        var refundRequestExists = await db.RefundRequests.AnyAsync(r => r.PaymentId == payment.Id);
        refundRequestExists.Should().BeFalse("chưa từng thanh toán thật thì không cần tạo yêu cầu hoàn tiền");
    }
}
