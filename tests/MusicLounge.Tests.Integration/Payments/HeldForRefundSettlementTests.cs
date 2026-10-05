using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.ValueObjects;
using MusicLounge.Infrastructure.Persistence;
using MusicLounge.Tests.Integration.Helpers;
using MusicLoungeVenue = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Tests.Integration.Payments;

/// <summary>
/// MLACP-662. Khoản quyết toán mà khoản thanh toán gốc còn yêu cầu hoàn tiền đang chờ thì SettlementReleaseJob giữ lại (không
/// chuyển). "Các đợt quyết toán gần đây" phải nói đúng như vậy — trước đây vẫn ghi "Đã lên lịch · dự kiến chuyển <ngày>"
/// (đo 05/10/2026: bốn khoản của buổi phát tự hoàn vì chỉ chạy 2% thời lượng). Mỗi bài một chủ phòng trà riêng.
/// </summary>
[Collection("Integration")]
public sealed class HeldForRefundSettlementTests
{
    private readonly ApiFactory _factory;

    public HeldForRefundSettlementTests(ApiFactory factory) => _factory = factory;

    private async Task<Guid> OwnerWithScheduledTrancheAsync(bool pendingRefund)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var owner = new User { Email = $"owner662-{Guid.NewGuid():N}@test.com", FullName = "Chủ phòng trà 662", Role = UserRole.Owner };
        db.Users.Add(owner);
        await db.SaveChangesAsync();
        var lounge = new MusicLoungeVenue
        {
            OwnerId = owner.Id, Name = $"Venue662-{Guid.NewGuid():N}"[..30], Status = LoungeStatus.Approved,
            Address = new VenueAddress { Street = "1 Test St", District = "1", City = "HCM" }
        };
        db.Lounges.Add(lounge);
        var start = DateTimeOffset.UtcNow.AddDays(-1);
        var show = new LoungeShow
        {
            LoungeId = lounge.Id, Name = "Đêm nhạc 662", Status = LoungeShowStatus.Ended,
            ScheduledStart = start, ScheduledEnd = start.AddHours(2), CreatedAt = DateTime.UtcNow
        };
        db.Add(show);
        await db.SaveChangesAsync();
        var payment = new Payment
        {
            OrderId = $"MLACP662-{Guid.NewGuid():N}"[..30], PayerId = SeedHelper.AudienceId, GrossAmount = 200_000m,
            NetAmount = 180_000m, Status = PaymentStatus.Confirmed, ReferenceType = "TicketHold", ReferenceId = "0",
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.Add(payment);
        await db.SaveChangesAsync();
        db.Add(new Ticket
        {
            Id = Guid.NewGuid(), BuyerId = SeedHelper.AudienceId, PriceId = SeedHelper.TicketPriceId, TierId = SeedHelper.TicketTierId,
            ShowId = show.Id, PaymentId = payment.Id, Status = TicketStatus.Confirmed, PurchaseChannel = PurchaseChannel.Online,
            CreatedAt = DateTimeOffset.UtcNow
        });
        db.Add(new Settlement
        {
            OwnerId = owner.Id, PaymentId = payment.Id, ReleaseType = SettlementReleaseType.Partial70,
            GrossAmount = 200_000m, PreRateApplied = 0.70m, PostRateApplied = 0.30m, NetAmount = 126_000m,
            Status = SettlementStatus.Scheduled, ScheduledAt = DateTimeOffset.UtcNow.AddDays(2), CreatedAt = DateTimeOffset.UtcNow
        });
        if (pendingRefund)
            db.Add(new RefundRequest
            {
                PaymentId = payment.Id, Reason = "Buổi phát sóng chỉ chạy 2% thời lượng đã bán — hoàn 100%",
                AmountRequested = 200_000m, Status = RefundRequestStatus.Pending
            });
        await db.SaveChangesAsync();
        return owner.Id;
    }

    private async Task<bool> HeldAsync(Guid ownerId)
    {
        var res = await _factory.CreateAuthenticatedClient(ownerId, "Owner").GetAsync("/api/v1/me/earnings");
        var body = await res.Content.ReadAsStringAsync();
        res.IsSuccessStatusCode.Should().BeTrue(body);
        return JsonDocument.Parse(body).RootElement.GetProperty("data").GetProperty("recentSettlements")
            .EnumerateArray().Single().GetProperty("heldForRefund").GetBoolean();
    }

    [Fact]
    public async Task ATrancheWhosePaymentAwaitsARefund_IsMarkedHeld()
        => (await HeldAsync(await OwnerWithScheduledTrancheAsync(pendingRefund: true)))
            .Should().BeTrue("the release job will not pay it while the refund is pending");

    [Fact]
    public async Task AnOrdinaryScheduledTranche_IsNotMarkedHeld()
        => (await HeldAsync(await OwnerWithScheduledTrancheAsync(pendingRefund: false))).Should().BeFalse();
}
