using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Infrastructure.Persistence;
using MusicLounge.Tests.Integration.Helpers;

namespace MusicLounge.Tests.Integration.CF7;

/// <summary>
/// Đợt-6 audit. ComplaintResolvedAction has five values and only two of them used to do anything:
/// TakeDownContent cancelled the show and refunded everyone, IssueWarning created a real
/// VenuePenalty (fixed at MLACP-198, whose own comment says IssueWarning had until then been "chi
/// la 1 nhan luu tren Complaint, khong co hau qua that nao"). Refund was left in exactly that state.
///
/// An Admin picking Refund stored the label, sent the complainant "Khiếu nại của bạn đã được xử lý",
/// and moved no money — while closing the complaint for good, since the handler refuses to resolve
/// an already-resolved one. Worse than doing nothing, because it manufactures an expectation.
///
/// Refund now means what TakeDownContent does not: refund the COMPLAINANT's own tickets and leave
/// the show running for everyone else.
/// </summary>
[Collection("Integration")]
public sealed class ComplaintRefundActionTests
{
    private readonly ApiFactory _factory;

    public ComplaintRefundActionTests(ApiFactory factory) => _factory = factory;

    private async Task<(int ComplaintId, int ShowId, int PaymentId)> SeedShowComplaintWithTicketAsync(
        int? complainantId = SeedHelper.AudienceId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var show = new LoungeShow
        {
            LoungeId = SeedHelper.LoungeId,
            Name = $"ComplaintShow-{Guid.NewGuid():N}",
            Description = "Integration test show",
            Format = LoungeShowFormat.Offline,
            Status = LoungeShowStatus.Published,
            ScheduledStart = DateTimeOffset.UtcNow.AddDays(2),
            ScheduledEnd = DateTimeOffset.UtcNow.AddDays(2).AddHours(3)
        };
        db.LoungeShows.Add(show);
        await db.SaveChangesAsync();

        var payment = new Payment
        {
            OrderId = $"CRA-{Guid.NewGuid():N}"[..30],
            GrossAmount = 300_000m,
            Status = PaymentStatus.Confirmed,
            ReferenceType = "TicketHold", ReferenceId = "0",
            PaidAt = DateTimeOffset.UtcNow.AddDays(-1),
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-1)
        };
        db.Payments.Add(payment);
        await db.SaveChangesAsync();

        db.Tickets.Add(new Ticket
        {
            Id = Guid.NewGuid(),
            BuyerId = SeedHelper.AudienceId,
            PriceId = SeedHelper.TicketPriceId,
            TierId = SeedHelper.TicketTierId,
            ShowId = show.Id,
            PaymentId = payment.Id,
            Status = TicketStatus.Confirmed,
            PurchaseChannel = PurchaseChannel.Online,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-1)
        });

        var complaint = new Complaint
        {
            ComplainantUserId = complainantId,
            TargetType = "show",
            TargetId = show.Id,
            Category = ComplaintCategory.EventMisrepresentation,
            Description = "Nội dung quảng cáo không đúng với thực tế",
            Status = ComplaintStatus.Open,
            CreatedAt = DateTimeOffset.UtcNow.AddHours(-2)
        };
        db.Set<Complaint>().Add(complaint);
        await db.SaveChangesAsync();

        return (complaint.Id, show.Id, payment.Id);
    }

    [Fact]
    public async Task ResolveWithRefund_CreatesPendingRefundForComplainantAndLeavesShowRunning()
    {
        var (complaintId, showId, paymentId) = await SeedShowComplaintWithTicketAsync();
        var adminClient = _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin");

        var res = await adminClient.PostAsJsonAsync(
            $"/api/v1/complaints/{complaintId}/resolve",
            new { Status = "Resolved", ResolvedAction = "Refund", Resolution = "Chấp nhận khiếu nại" });
        res.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var refunds = await db.RefundRequests.Where(r => r.PaymentId == paymentId).ToListAsync();
        refunds.Should().HaveCount(1,
            "choosing Refund must actually raise a refund request, not just record the word");
        refunds[0].Status.Should().Be(RefundRequestStatus.Pending);
        refunds[0].RequestedBy.Should().Be(SeedHelper.AudienceId);

        var tickets = await db.Tickets.Where(t => t.PaymentId == paymentId).ToListAsync();
        tickets.Should().AllSatisfy(t => t.Status.Should().Be(TicketStatus.Cancelled));

        var show = await db.LoungeShows.SingleAsync(s => s.Id == showId);
        show.Status.Should().Be(LoungeShowStatus.Published,
            "Refund compensates one complainant — cancelling the show for everyone is what " +
            "TakeDownContent is for");
    }

    [Fact]
    public async Task ResolveWithRefund_ForGuestComplainant_IsRefusedRatherThanSilentlyDoingNothing()
    {
        var (complaintId, _, paymentId) = await SeedShowComplaintWithTicketAsync(complainantId: null);
        var adminClient = _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin");

        var res = await adminClient.PostAsJsonAsync(
            $"/api/v1/complaints/{complaintId}/resolve",
            new { Status = "Resolved", ResolvedAction = "Refund", Resolution = "Chấp nhận khiếu nại" });

        res.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "a guest complaint names no account, so there are no tickets to refund — the Admin must " +
            "be told that instead of the complaint closing with nothing having happened");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        (await db.RefundRequests.AnyAsync(r => r.PaymentId == paymentId)).Should().BeFalse();
        var complaint = await db.Set<Complaint>().SingleAsync(c => c.Id == complaintId);
        complaint.Status.Should().Be(ComplaintStatus.Open,
            "the complaint must stay open so it can still be resolved a workable way");
    }

    [Fact]
    public async Task ResolveWithCompensate_IsNoLongerAValidAction()
    {
        // "Compensate" existed as an enum value that did nothing: it stored a label, told the
        // complainant their case was handled, and moved no money — while closing the complaint for
        // good. Removed in MLACP-286 rather than implemented, because there is no payout channel to
        // an audience member at all and the one plausible form (wallet credit) is exactly what got
        // StubHub fined. An Admin sending it must now get a clear rejection, not a silent 204.
        var (complaintId, _, paymentId) = await SeedShowComplaintWithTicketAsync();
        var adminClient = _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin");

        var res = await adminClient.PostAsJsonAsync(
            $"/api/v1/complaints/{complaintId}/resolve",
            new { Status = "Resolved", ResolvedAction = "Compensate", Resolution = "Bồi thường cho khách" });

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "an action the system cannot carry out must be refused at the door");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var complaint = await db.Set<Complaint>().SingleAsync(c => c.Id == complaintId);
        complaint.Status.Should().Be(ComplaintStatus.Open,
            "the complaint must stay open so it can be resolved a way that actually does something");
        (await db.RefundRequests.AnyAsync(r => r.PaymentId == paymentId)).Should().BeFalse();
    }
}
