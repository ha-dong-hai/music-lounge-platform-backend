using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Infrastructure.Persistence;
using MusicLounge.Tests.Integration.Helpers;

namespace MusicLounge.Tests.Integration.CF3;

/// <summary>
/// W26a — Admin xử lý RefundRequest (đảo bút toán ledger, D8)
/// GET  /api/v1/admin/refund-requests
/// POST /api/v1/admin/refund-requests/{id}/process
/// </summary>
[Collection("Integration")]
public sealed class RefundProcessingTests
{
    private readonly ApiFactory _factory;

    public RefundProcessingTests(ApiFactory factory) => _factory = factory;

    /// <summary>Confirmed ticket + Payment (with realistic fee split) + Pending RefundRequest.</summary>
    private async Task<(int RefundRequestId, int PaymentId)> SeedPendingRefundRequestAsync(
        decimal gross = 100_000m, decimal platformFee = 5_000m, decimal tax = 5_000m)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var payment = new Payment
        {
            OrderId = $"RF-{Guid.NewGuid():N}"[..30],
            GrossAmount = gross,
            PlatformFee = platformFee,
            TaxWithheld = tax,
            NetAmount = gross - platformFee - tax,
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
            Status = TicketStatus.Cancelled,
            PurchaseChannel = PurchaseChannel.Online,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.Add(ticket);
        await db.SaveChangesAsync();

        var refund = new RefundRequest
        {
            PaymentId = payment.Id,
            RequestedBy = SeedHelper.AudienceId,
            Reason = "Test cancellation",
            AmountRequested = gross,
            RefundPercentage = 100m,
            Status = RefundRequestStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.Add(refund);
        await db.SaveChangesAsync();

        return (refund.Id, payment.Id);
    }

    [Fact]
    public async Task ProcessRefundRequest_Approve_ReversesLedgerAndSetsPaymentRefunded()
    {
        var (refundId, paymentId) = await SeedPendingRefundRequestAsync();
        var client = _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin");

        var res = await client.PostAsJsonAsync(
            $"/api/v1/admin/refund-requests/{refundId}/process",
            new { Decision = "Approved", ApprovedAmount = 100_000m });

        res.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var refund = await db.RefundRequests.FindAsync(refundId);
        refund!.Status.Should().Be(RefundRequestStatus.Approved);
        refund.AmountApproved.Should().Be(100_000m);

        var payment = await db.Payments.FindAsync(paymentId);
        payment!.Status.Should().Be(PaymentStatus.Refunded);

        var entries = db.LedgerEntries.Where(e => e.PaymentId == paymentId && e.ReferenceType == "refund").ToList();
        entries.Should().NotBeEmpty();
        entries.Where(e => e.IsDebit).Sum(e => e.Amount)
            .Should().Be(entries.Where(e => !e.IsDebit).Sum(e => e.Amount));
    }

    [Fact]
    public async Task ProcessRefundRequest_Reject_LeavesPaymentAndLedgerUntouched()
    {
        var (refundId, paymentId) = await SeedPendingRefundRequestAsync();
        var client = _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin");

        var res = await client.PostAsJsonAsync(
            $"/api/v1/admin/refund-requests/{refundId}/process",
            new { Decision = "Rejected", ApprovedAmount = (decimal?)null });

        res.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var refund = await db.RefundRequests.FindAsync(refundId);
        refund!.Status.Should().Be(RefundRequestStatus.Rejected);

        var payment = await db.Payments.FindAsync(paymentId);
        payment!.Status.Should().Be(PaymentStatus.Confirmed);

        db.LedgerEntries.Any(e => e.PaymentId == paymentId).Should().BeFalse();
    }

    [Fact]
    public async Task ProcessRefundRequest_AlreadyProcessed_Returns409()
    {
        var (refundId, _) = await SeedPendingRefundRequestAsync();
        var client = _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin");
        await client.PostAsJsonAsync(
            $"/api/v1/admin/refund-requests/{refundId}/process",
            new { Decision = "Approved", ApprovedAmount = 100_000m });

        var res = await client.PostAsJsonAsync(
            $"/api/v1/admin/refund-requests/{refundId}/process",
            new { Decision = "Approved", ApprovedAmount = 100_000m });

        res.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    /// <summary>
    /// Regression test for MLACP-251 (audit-flagged C3 gap, 2026-09-04): this handler is the only
    /// one in the whole Ticket/Refund domain that calls a real external payment API (VNPay refund)
    /// but previously had no lock around its "Status == Pending" check-then-act — two genuinely
    /// concurrent Approve calls for the same RefundRequestId could both read Pending before either
    /// committed, both calling VNPay's real refund API. Fires both requests truly in parallel
    /// (Task.WhenAll, not sequential awaits) to actually open the race window.
    /// </summary>
    [Fact]
    public async Task ProcessRefundRequest_TwoConcurrentApprovals_OnlyOneSucceeds()
    {
        var (refundId, paymentId) = await SeedPendingRefundRequestAsync();
        var client = _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin");

        var call1 = client.PostAsJsonAsync(
            $"/api/v1/admin/refund-requests/{refundId}/process",
            new { Decision = "Approved", ApprovedAmount = 100_000m });
        var call2 = client.PostAsJsonAsync(
            $"/api/v1/admin/refund-requests/{refundId}/process",
            new { Decision = "Approved", ApprovedAmount = 100_000m });

        var results = await Task.WhenAll(call1, call2);

        results.Count(r => r.StatusCode == HttpStatusCode.NoContent).Should().Be(1,
            "only one of two genuinely concurrent approvals for the same refund request may succeed");
        results.Count(r => r.StatusCode == HttpStatusCode.Conflict).Should().Be(1,
            "the loser of the race must see 409, not silently succeed or crash");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var entries = db.LedgerEntries.Where(e => e.PaymentId == paymentId && e.ReferenceType == "refund").ToList();
        entries.Where(e => e.IsDebit).Sum(e => e.Amount)
            .Should().Be(100_000m, "the refund must be journaled exactly once, not twice");
    }

    [Fact]
    public async Task ProcessRefundRequest_AsNonAdmin_Returns403()
    {
        var (refundId, _) = await SeedPendingRefundRequestAsync();
        var client = _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner");

        var res = await client.PostAsJsonAsync(
            $"/api/v1/admin/refund-requests/{refundId}/process",
            new { Decision = "Approved", ApprovedAmount = 100_000m });

        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetPendingRefundRequests_AsAdmin_ContainsSeededOne()
    {
        await SeedPendingRefundRequestAsync();
        var client = _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin");

        var res = await client.GetAsync("/api/v1/admin/refund-requests");

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await res.Content.ReadAsStringAsync();
        body.Should().Contain("\"status\":\"Pending\"");
    }

    // ─── GET /tickets/refund-requests/my ────────────────────────────────────
    // Added during REST-standards review — CancelTicket returns refundRequestId in its response,
    // but before this the buyer had no way to read it back: /admin/refund-requests is Admin-only,
    // and TicketDetailDto carries no refund status at all.

    [Fact]
    public async Task GetMyRefundRequests_AsRequester_ContainsSeededOne()
    {
        await SeedPendingRefundRequestAsync();
        var client = _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience");

        var res = await client.GetAsync("/api/v1/tickets/refund-requests/my");

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await res.Content.ReadAsStringAsync();
        body.Should().Contain("\"status\":\"Pending\"");
    }

    [Fact]
    public async Task GetMyRefundRequests_AsDifferentUser_DoesNotContainOthersRequest()
    {
        var (refundId, _) = await SeedPendingRefundRequestAsync(); // RequestedBy = AudienceId
        var client = _factory.CreateAuthenticatedClient(SeedHelper.OtherOwnerId, "Owner");

        var res = await client.GetAsync("/api/v1/tickets/refund-requests/my");

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await res.Content.ReadAsStringAsync();
        body.Should().NotContain($"\"id\":{refundId}", "must only return the caller's own refund requests");
    }

    [Fact]
    public async Task GetMyRefundRequests_Unauthenticated_Returns401()
    {
        var client = _factory.CreateClient();

        var res = await client.GetAsync("/api/v1/tickets/refund-requests/my");

        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
