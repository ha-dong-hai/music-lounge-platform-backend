using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Infrastructure.Jobs;
using MusicLounge.Infrastructure.Persistence;
using MusicLounge.Tests.Integration.Helpers;

namespace MusicLounge.Tests.Integration.CF3;

/// <summary>
/// W26b — Chuyển nhượng vé (initiate → accept/reject/cancel)
/// POST /api/v1/tickets/{id}/transfer | .../transfer/accept | .../transfer/cancel
/// </summary>
[Collection("Integration")]
public sealed class TicketTransferTests
{
    private readonly ApiFactory _factory;

    public TicketTransferTests(ApiFactory factory) => _factory = factory;

    private async Task<Guid> SeedConfirmedTicketAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var payment = new Payment
        {
            OrderId = $"TR-{Guid.NewGuid():N}"[..30],
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
            PriceId = SeedHelper.TicketPriceId,
            TierId = SeedHelper.TicketTierId,
            ShowId = SeedHelper.ShowId,
            PaymentId = payment.Id,
            Status = TicketStatus.Confirmed,
            QrCode = $"QR-{Guid.NewGuid():N}",
            PurchaseChannel = PurchaseChannel.Online,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.Add(ticket);
        await db.SaveChangesAsync();

        return ticket.Id;
    }

    [Fact]
    public async Task InitiateTransfer_ByBuyer_Returns204()
    {
        var ticketId = await SeedConfirmedTicketAsync();
        var client = _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience");

        var res = await client.PostAsJsonAsync(
            $"/api/v1/tickets/{ticketId}/transfer", new { RecipientEmail = "owner2@test.com" });

        res.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task InitiateTransfer_ByNonBuyer_Returns403()
    {
        var ticketId = await SeedConfirmedTicketAsync();
        var client = _factory.CreateAuthenticatedClient(SeedHelper.OtherOwnerId, "Owner");

        var res = await client.PostAsJsonAsync(
            $"/api/v1/tickets/{ticketId}/transfer", new { RecipientEmail = "owner2@test.com" });

        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task InitiateTransfer_ToSelf_Returns422()
    {
        var ticketId = await SeedConfirmedTicketAsync();
        var client = _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience");

        var res = await client.PostAsJsonAsync(
            $"/api/v1/tickets/{ticketId}/transfer", new { RecipientEmail = "audience@test.com" });

        res.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task InitiateTransfer_WhilePending_Returns409()
    {
        var ticketId = await SeedConfirmedTicketAsync();
        var client = _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience");
        await client.PostAsJsonAsync($"/api/v1/tickets/{ticketId}/transfer", new { RecipientEmail = "owner2@test.com" });

        var res = await client.PostAsJsonAsync(
            $"/api/v1/tickets/{ticketId}/transfer", new { RecipientEmail = "owner2@test.com" });

        res.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task CancelOrCheckIn_WhileTransferPending_Returns422()
    {
        var ticketId = await SeedConfirmedTicketAsync();
        var buyerClient = _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience");
        await buyerClient.PostAsJsonAsync($"/api/v1/tickets/{ticketId}/transfer", new { RecipientEmail = "owner2@test.com" });

        var cancelRes = await buyerClient.PostAsync($"/api/v1/tickets/{ticketId}/cancel", null);

        cancelRes.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task GetIncomingTransfers_ForRecipient_ContainsInitiatedTransfer()
    {
        var ticketId = await SeedConfirmedTicketAsync();
        var buyerClient = _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience");
        await buyerClient.PostAsJsonAsync($"/api/v1/tickets/{ticketId}/transfer", new { RecipientEmail = "owner2@test.com" });

        var recipientClient = _factory.CreateAuthenticatedClient(SeedHelper.OtherOwnerId, "Owner");
        var res = await recipientClient.GetAsync("/api/v1/tickets/incoming-transfers");

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await res.Content.ReadAsStringAsync();
        body.Should().Contain(ticketId.ToString());
    }

    [Fact]
    public async Task AcceptTransfer_ByCorrectRecipient_ChangesOwnerAndRegeneratesQr()
    {
        var ticketId = await SeedConfirmedTicketAsync();
        string? originalQr;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            originalQr = (await db.Tickets.FindAsync(ticketId))!.QrCode;
        }

        var buyerClient = _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience");
        await buyerClient.PostAsJsonAsync($"/api/v1/tickets/{ticketId}/transfer", new { RecipientEmail = "owner2@test.com" });

        var recipientClient = _factory.CreateAuthenticatedClient(SeedHelper.OtherOwnerId, "Owner");
        var res = await recipientClient.PostAsync($"/api/v1/tickets/{ticketId}/transfer/accept", null);

        res.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var ticket = await verifyDb.Tickets.FindAsync(ticketId);
        ticket!.BuyerId.Should().Be(SeedHelper.OtherOwnerId);
        ticket.QrCode.Should().NotBe(originalQr);
        ticket.PendingTransferToUserId.Should().BeNull();
    }

    [Fact]
    public async Task AcceptTransfer_ByWrongUser_Returns403()
    {
        var ticketId = await SeedConfirmedTicketAsync();
        var buyerClient = _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience");
        await buyerClient.PostAsJsonAsync($"/api/v1/tickets/{ticketId}/transfer", new { RecipientEmail = "owner2@test.com" });

        var wrongClient = _factory.CreateAuthenticatedClient(SeedHelper.StaffId, "Staff");
        var res = await wrongClient.PostAsync($"/api/v1/tickets/{ticketId}/transfer/accept", null);

        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CancelTransfer_BySender_ClearsPendingAndAllowsNormalCancelAgain()
    {
        var ticketId = await SeedConfirmedTicketAsync();
        var buyerClient = _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience");
        await buyerClient.PostAsJsonAsync($"/api/v1/tickets/{ticketId}/transfer", new { RecipientEmail = "owner2@test.com" });

        var cancelTransferRes = await buyerClient.PostAsync($"/api/v1/tickets/{ticketId}/transfer/cancel", null);
        cancelTransferRes.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var ticket = await db.Tickets.FindAsync(ticketId);
            ticket!.PendingTransferToUserId.Should().BeNull();
            ticket.BuyerId.Should().Be(SeedHelper.AudienceId);
        }

        var cancelTicketRes = await buyerClient.PostAsync($"/api/v1/tickets/{ticketId}/cancel", null);
        cancelTicketRes.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CancelTransfer_ByRecipient_RejectsTransfer()
    {
        var ticketId = await SeedConfirmedTicketAsync();
        var buyerClient = _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience");
        await buyerClient.PostAsJsonAsync($"/api/v1/tickets/{ticketId}/transfer", new { RecipientEmail = "owner2@test.com" });

        var recipientClient = _factory.CreateAuthenticatedClient(SeedHelper.OtherOwnerId, "Owner");
        var res = await recipientClient.PostAsync($"/api/v1/tickets/{ticketId}/transfer/cancel", null);

        res.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var ticket = await db.Tickets.FindAsync(ticketId);
        ticket!.PendingTransferToUserId.Should().BeNull();
        ticket.BuyerId.Should().Be(SeedHelper.AudienceId);
    }

    // ─── W26b TicketTransferExpiryJob — master-backend-techlead review ────────
    // Found via the same audit pattern as the lounges/recommendations 500s: this job had zero
    // test coverage, and its query combined a `!= null` predicate with a DateTimeOffset
    // comparison in one Where clause — the same SQLite-translation limitation documented
    // repeatedly elsewhere in this codebase. See TicketTransferExpiryJob for the fix.

    [Fact]
    public async Task TicketTransferExpiryJob_PastDeadline_ClearsPendingFields()
    {
        var ticketId = await SeedConfirmedTicketAsync();
        var client = _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience");
        await client.PostAsJsonAsync($"/api/v1/tickets/{ticketId}/transfer", new { RecipientEmail = "owner2@test.com" });

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var ticket = await db.Tickets.FindAsync(ticketId);
            ticket!.PendingTransferInitiatedAt = DateTimeOffset.UtcNow.AddHours(-49); // past the 48h window
            await db.SaveChangesAsync();
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var job = scope.ServiceProvider.GetRequiredService<TicketTransferExpiryJob>();
            await job.ExecuteAsync();
        }

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var updated = await verifyDb.Tickets.FindAsync(ticketId);
        updated!.PendingTransferToUserId.Should().BeNull("48h window elapsed with no response — must auto-expire, not stay frozen forever");
        updated.PendingTransferInitiatedAt.Should().BeNull();
        updated.BuyerId.Should().Be(SeedHelper.AudienceId, "ticket stays with the original owner, it isn't deleted or transferred");
    }

    [Fact]
    public async Task TicketTransferExpiryJob_WithinWindow_LeavesTransferPending()
    {
        var ticketId = await SeedConfirmedTicketAsync();
        var client = _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience");
        await client.PostAsJsonAsync($"/api/v1/tickets/{ticketId}/transfer", new { RecipientEmail = "owner2@test.com" });

        using (var scope = _factory.Services.CreateScope())
        {
            var job = scope.ServiceProvider.GetRequiredService<TicketTransferExpiryJob>();
            await job.ExecuteAsync();
        }

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var ticket = await verifyDb.Tickets.FindAsync(ticketId);
        ticket!.PendingTransferToUserId.Should().NotBeNull("just initiated — nowhere near the 48h expiry");
    }
}
