using FluentAssertions;
using Hangfire;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Infrastructure.Jobs;
using MusicLounge.Infrastructure.Persistence;
using MusicLounge.Tests.Integration.Helpers;

namespace MusicLounge.Tests.Integration.CF6;

/// <summary>
/// Đợt-7 audit. Complaints had SLA tracking; refunds — where the buyer's money is actually meant to
/// come back — had none. A RefundRequest sat Pending indefinitely, nothing chased it, and the buyer
/// had no way to know how long to wait.
///
/// Two deadlines, deliberately different in kind:
///   refund_sla_hours (72h) — an operational promise, aligned with Điều 31 Luật Bảo vệ quyền lợi
///     người tiêu dùng 2023 (acknowledge within 03 business days) and stricter than Eventbrite's
///     5 business days.
///   vnpay_refund_window_days (90) — a hard wall. VNPay refuses a reversal past ~3 months from the
///     transaction, so crossing it makes the refund impossible through the gateway rather than
///     merely late.
/// </summary>
[Collection("Integration")]
public sealed class RefundSlaTests
{
    private readonly ApiFactory _factory;

    public RefundSlaTests(ApiFactory factory) => _factory = factory;

    private async Task<int> SeedPendingRefundAsync(DateTimeOffset paidAt, DateTime createdAt)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var payment = new Payment
        {
            OrderId = $"SLA-{Guid.NewGuid():N}"[..30],
            GrossAmount = 200_000m,
            Status = PaymentStatus.Confirmed,
            ReferenceType = "TicketHold", ReferenceId = "0",
            PaidAt = paidAt, CreatedAt = paidAt
        };
        db.Payments.Add(payment);
        await db.SaveChangesAsync();

        var refund = new RefundRequest
        {
            PaymentId = payment.Id,
            RequestedBy = SeedHelper.AudienceId,
            Reason = "Khách yêu cầu hủy vé",
            AmountRequested = 200_000m,
            RefundPercentage = 100m,
            Status = RefundRequestStatus.Pending
        };
        db.RefundRequests.Add(refund);
        await db.SaveChangesAsync();

        // CreatedAt is stamped by SaveChanges, so backdate it afterwards to age the request.
        refund.CreatedAt = createdAt;
        await db.SaveChangesAsync();

        return refund.Id;
    }

    private async Task<int> CountAdminAlertsAsync(int refundId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Notifications.CountAsync(
            n => n.Type == NotificationType.RefundSlaBreached
                 && n.ReferenceType == "refund_request"
                 && n.ReferenceId == refundId.ToString());
    }

    private async Task RunJobAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var job = scope.ServiceProvider.GetRequiredService<RefundSlaBreachAlertJob>();
        await job.ExecuteAsync(new JobCancellationToken(false));
    }

    [Fact]
    public async Task PendingRefundPastSla_AlertsAdmins()
    {
        var refundId = await SeedPendingRefundAsync(
            paidAt: DateTimeOffset.UtcNow.AddDays(-4),
            createdAt: DateTime.UtcNow.AddHours(-100));   // > 72h

        await RunJobAsync();

        (await CountAdminAlertsAsync(refundId)).Should().BeGreaterThan(0,
            "a refund the buyer is still waiting on must chase somebody, not sit silently");
    }

    [Fact]
    public async Task PendingRefundWithinSla_StaysQuiet()
    {
        var refundId = await SeedPendingRefundAsync(
            paidAt: DateTimeOffset.UtcNow.AddDays(-1),
            createdAt: DateTime.UtcNow.AddHours(-2));     // well inside 72h

        await RunJobAsync();

        (await CountAdminAlertsAsync(refundId)).Should().Be(0,
            "alerting on requests that are still within the promised window would train Admins to " +
            "ignore the alert");
    }

    [Fact]
    public async Task ProcessRefund_PastVnPayWindow_IsRefusedWithAnActionableMessage()
    {
        // Paid 100 days ago — beyond VNPay's ~3-month reversal window.
        var refundId = await SeedPendingRefundAsync(
            paidAt: DateTimeOffset.UtcNow.AddDays(-100),
            createdAt: DateTime.UtcNow.AddDays(-100));

        var adminClient = _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin");
        var res = await adminClient.PostAsJsonAsync(
            $"/api/v1/admin/refund-requests/{refundId}/process",
            new { Decision = "Approved", ApprovedAmount = (decimal?)null });

        res.StatusCode.Should().Be(System.Net.HttpStatusCode.UnprocessableEntity);
        var body = await res.Content.ReadAsStringAsync();
        body.Should().Contain("chuyển khoản thủ công",
            "the Admin must be told what to actually do, not handed a bare gateway error code");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var refund = await db.RefundRequests.SingleAsync(r => r.Id == refundId);
        refund.Status.Should().Be(RefundRequestStatus.Pending,
            "the buyer is still owed this money — the request must stay open, not close as handled");
    }
}
