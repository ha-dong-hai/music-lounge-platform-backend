using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Infrastructure.Persistence;
using MusicLounge.Tests.Integration.Helpers;

namespace MusicLounge.Tests.Integration.Users;

/// <summary>
/// GET /api/v1/me/earnings — master-backend-techlead review found PendingReview settlements
/// (D16: Final30 tranche whose actual/scheduled show-duration ratio missed the completion
/// threshold, parked for Admin to decide) were silently excluded from every total in this
/// summary — the money isn't lost, but it vanishes from the Owner's own earnings dashboard.
/// </summary>
[Collection("Integration")]
public sealed class GetMyEarningsTests
{
    private readonly ApiFactory _factory;

    public GetMyEarningsTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task PendingReviewSettlement_IsCountedInPendingSettlement()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var payment = new Payment
            {
                OrderId = $"EARN-{Guid.NewGuid():N}"[..30], GrossAmount = 500_000m,
                Status = PaymentStatus.Confirmed, ReferenceType = "TicketHold", ReferenceId = "0",
                CreatedAt = DateTimeOffset.UtcNow
            };
            db.Add(payment);
            await db.SaveChangesAsync();

            db.Add(new Settlement
            {
                OwnerId = SeedHelper.OwnerId, PaymentId = payment.Id,
                ReleaseType = SettlementReleaseType.Final30,
                GrossAmount = 500_000m, PreRateApplied = 0.70m, PostRateApplied = 0.30m,
                NetAmount = 150_000m, Status = SettlementStatus.PendingReview,
                ScheduledAt = DateTimeOffset.UtcNow.AddDays(-1), CreatedAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();
        }

        var client = _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner");
        var res = await client.GetAsync("/api/v1/me/earnings");
        res.EnsureSuccessStatusCode();

        var body = await res.Content.ReadFromJsonAsync<EarningsResponse>();
        body!.Data.PendingSettlement.Should().BeGreaterThanOrEqualTo(150_000m,
            "a PendingReview settlement is still money owed to the Owner and must count as pending, not vanish from the summary");
        body.Data.PendingSettlementCount.Should().BeGreaterThanOrEqualTo(1);
    }

    private sealed record EarningsResponse(bool Success, EarningsData Data);

    private sealed record EarningsData(
        decimal TotalEarned, decimal PendingSettlement, decimal CompletedSettlement,
        int PendingSettlementCount);
}
