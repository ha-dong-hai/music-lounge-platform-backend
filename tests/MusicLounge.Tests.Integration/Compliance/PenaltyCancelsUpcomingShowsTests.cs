using FluentAssertions;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.ValueObjects;
using MusicLounge.Infrastructure.Jobs;
using MusicLounge.Infrastructure.Persistence;
using MusicLounge.Tests.Integration.Helpers;
using MusicLoungeVenue = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Tests.Integration.Compliance;

/// <summary>
/// MLACP-373. Khi lệnh khoá vĩnh viễn / tạm khoá có hiệu lực, <c>ApplyDuePenaltiesJob</c> chỉ đổi trạng thái phòng
/// trà: vé đã bán cho các buổi diễn sắp tới vẫn nguyên — người mua tới một buổi diễn không còn ai tổ chức, không ai
/// được hoàn, không ai được báo (MLACP-354 ghi đây là điểm còn mở). Ticketbox: nhà tổ chức vi phạm → khoá tài khoản,
/// gỡ toàn bộ tin, yêu cầu bồi hoàn cho khách.
///
/// <para>Nay (đã chốt): khoá vĩnh viễn huỷ mọi buổi chưa diễn; tạm khoá huỷ các buổi bắt đầu trong thời gian bị khoá —
/// qua đúng đường huỷ của chủ phòng trà (hoàn 100%, yêu cầu hoàn đứng tên người đã trả, báo người giữ vé). Án đang
/// kháng cáo chưa có hiệu lực thì chưa huỷ gì.</para>
///
/// <para>Mỗi bài một phòng trà riêng: job áp MỌI án đến hạn trong cơ sở dữ liệu dùng chung.</para>
/// </summary>
[Collection("Integration")]
public sealed class PenaltyCancelsUpcomingShowsTests
{
    private const decimal Price = 150_000m;

    private readonly ApiFactory _factory;

    public PenaltyCancelsUpcomingShowsTests(ApiFactory factory) => _factory = factory;

    private sealed record Venue(int OwnerId, int LoungeId);

    private async Task<Venue> VenueAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var owner = new User { Email = $"pen373-{Guid.NewGuid():N}@test.com", FullName = "Chủ phòng trà" };
        db.Users.Add(owner);
        await db.SaveChangesAsync();

        var lounge = new MusicLoungeVenue
        {
            OwnerId = owner.Id, Name = $"Venue373-{Guid.NewGuid():N}"[..30], Status = LoungeStatus.Approved,
            Address = new VenueAddress { Street = "1 Test St", District = "1", City = "HCM" }
        };
        db.Lounges.Add(lounge);
        await db.SaveChangesAsync();
        return new Venue(owner.Id, lounge.Id);
    }

    private async Task<int> UserAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = new User { Email = $"buyer373-{Guid.NewGuid():N}@test.com", FullName = "Người mua" };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }

    private async Task<int> ShowAsync(int loungeId, DateTimeOffset start, LoungeShowStatus status = LoungeShowStatus.Published)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var show = new LoungeShow
        {
            LoungeId = loungeId,
            Name = $"Đêm nhạc {Guid.NewGuid():N}"[..18],
            Description = "MLACP-373",
            Format = LoungeShowFormat.Offline,
            Status = status,
            ScheduledStart = start,
            ScheduledEnd = start.AddHours(3),
            CreatedAt = DateTime.UtcNow
        };
        db.Add(show);
        await db.SaveChangesAsync();
        return show.Id;
    }

    /// <param name="payerId">Người đã trả tiền; null = vé bán tại quầy (tiền mặt, không có tài khoản).</param>
    private async Task<(Guid TicketId, int PaymentId)> TicketAsync(int showId, int? payerId, int? holderId = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var walkIn = payerId is null;

        var tier = new TicketTier
        {
            LoungeShowId = showId, Name = "Vào cửa", AccessType = AccessType.Physical, CreatedAt = DateTime.UtcNow
        };
        db.Add(tier);
        await db.SaveChangesAsync();

        var price = new TicketPrice
        {
            TierId = tier.Id, Name = "Đợt 1", Price = Price,
            PurchaseChannel = walkIn ? PurchaseChannel.Offline : PurchaseChannel.Online,
            SaleStart = DateTimeOffset.UtcNow.AddDays(-30)
        };
        db.Add(price);
        await db.SaveChangesAsync();

        var payment = new Payment
        {
            OrderId = $"MLACP373-{Guid.NewGuid():N}"[..30],
            PayerId = payerId,
            GrossAmount = Price,
            NetAmount = Price,
            Method = walkIn ? PaymentMethod.Cash : PaymentMethod.Gateway,
            Status = PaymentStatus.Confirmed,
            ReferenceType = walkIn ? "WalkIn" : "TicketHold",
            ReferenceId = "0",
            TransactionId = walkIn ? null : $"V{Guid.NewGuid():N}"[..16],
            PaidAt = DateTimeOffset.UtcNow.AddDays(-1),
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-1)
        };
        db.Add(payment);
        await db.SaveChangesAsync();

        var ticket = new Ticket
        {
            Id = Guid.NewGuid(),
            BuyerId = holderId ?? payerId,
            PriceId = price.Id,
            TierId = tier.Id,
            ShowId = showId,
            PaymentId = payment.Id,
            Status = TicketStatus.Confirmed,
            PurchaseChannel = walkIn ? PurchaseChannel.Offline : PurchaseChannel.Online,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-1)
        };
        db.Add(ticket);
        await db.SaveChangesAsync();
        return (ticket.Id, payment.Id);
    }

    private async Task PenaltyAsync(int loungeId, PenaltyType type, PenaltyStatus status = PenaltyStatus.Active)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.VenuePenalties.Add(new VenuePenalty
        {
            LoungeId = loungeId, PenaltyType = type, Reason = "Vi phạm nghiêm trọng",
            IssuedBy = SeedHelper.AdminId, IssuedAt = DateTimeOffset.UtcNow.AddDays(-8),
            EffectiveAt = DateTimeOffset.UtcNow.AddMinutes(-1),
            SuspensionDays = type == PenaltyType.Suspension ? 7 : null,
            Status = status,
            AppealedAt = status == PenaltyStatus.Appealed ? DateTimeOffset.UtcNow.AddHours(-1) : null
        });
        await db.SaveChangesAsync();
    }

    private async Task ApplyDuePenaltiesAsync()
    {
        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ApplyDuePenaltiesJob>().ExecuteAsync(new JobCancellationToken(false));
    }

    private async Task<LoungeShowStatus> ShowStatusAsync(int showId)
    {
        using var scope = _factory.Services.CreateScope();
        return (await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().LoungeShows.AsNoTracking()
            .SingleAsync(s => s.Id == showId)).Status;
    }

    private async Task<TicketStatus> TicketStatusAsync(Guid ticketId)
    {
        using var scope = _factory.Services.CreateScope();
        return (await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Tickets.AsNoTracking()
            .SingleAsync(t => t.Id == ticketId)).Status;
    }

    private async Task<List<RefundRequest>> RefundsAsync(int paymentId)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().RefundRequests.AsNoTracking()
            .Where(r => r.PaymentId == paymentId).ToListAsync();
    }

    private async Task<List<Notification>> NoticesAsync(int userId, NotificationType type)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Notifications.AsNoTracking()
            .Where(n => n.UserId == userId && n.Type == type).ToListAsync();
    }

    // ── Khoá vĩnh viễn ───────────────────────────────────────────────────────

    [Fact]
    public async Task ABan_CancelsEveryUpcomingShow_RefundsItsBuyersInFull_AndTellsEveryone()
    {
        var venue = await VenueAsync();
        var buyer = await UserAsync();
        var soon = await ShowAsync(venue.LoungeId, DateTimeOffset.UtcNow.AddDays(3));
        var later = await ShowAsync(venue.LoungeId, DateTimeOffset.UtcNow.AddDays(60));
        var (soonTicket, soonPayment) = await TicketAsync(soon, buyer);
        var (_, laterPayment) = await TicketAsync(later, buyer);
        await PenaltyAsync(venue.LoungeId, PenaltyType.Ban);

        await ApplyDuePenaltiesAsync();

        (await ShowStatusAsync(soon)).Should().Be(LoungeShowStatus.Cancelled,
            "nobody will put this show on — its buyers must not find that out at the door");
        (await ShowStatusAsync(later)).Should().Be(LoungeShowStatus.Cancelled, "a ban has no end date");
        (await TicketStatusAsync(soonTicket)).Should().Be(TicketStatus.Cancelled);
        foreach (var payment in new[] { soonPayment, laterPayment })
        {
            var refund = (await RefundsAsync(payment)).Should().ContainSingle().Subject;
            refund.RefundPercentage.Should().Be(100m);
            refund.AmountRequested.Should().Be(Price);
            refund.RequestedBy.Should().Be(buyer);
        }

        (await NoticesAsync(buyer, NotificationType.EventCancelled))
            .Should().Contain(n => n.ReferenceId == soon.ToString() && n.Body.Contains("ngừng hoạt động trên nền tảng"),
                "the buyer is told why, in the same neutral words the purchase gate uses");
        (await NoticesAsync(venue.OwnerId, NotificationType.PenaltyIssued))
            .Should().Contain(n => n.Body.Contains("Đã huỷ 2 buổi diễn"), "the owner learns what the ban did to their shows");
    }

    [Fact]
    public async Task ABan_LeavesPastAndRunningShows_AndOtherVenues_Alone()
    {
        var venue = await VenueAsync();
        var other = await VenueAsync();
        var buyer = await UserAsync();
        var ended = await ShowAsync(venue.LoungeId, DateTimeOffset.UtcNow.AddDays(-2), LoungeShowStatus.Ended);
        var running = await ShowAsync(venue.LoungeId, DateTimeOffset.UtcNow.AddHours(-1));
        var elsewhere = await ShowAsync(other.LoungeId, DateTimeOffset.UtcNow.AddDays(3));
        var (_, runningPayment) = await TicketAsync(running, buyer);
        var (_, elsewherePayment) = await TicketAsync(elsewhere, buyer);
        await PenaltyAsync(venue.LoungeId, PenaltyType.Ban);

        await ApplyDuePenaltiesAsync();

        (await ShowStatusAsync(ended)).Should().Be(LoungeShowStatus.Ended);
        (await ShowStatusAsync(running)).Should().Be(LoungeShowStatus.Published,
            "a show past its start belongs to the undelivered-show path (MLACP-338), not to this one");
        (await ShowStatusAsync(elsewhere)).Should().Be(LoungeShowStatus.Published, "another venue did nothing wrong");
        (await RefundsAsync(runningPayment)).Should().BeEmpty();
        (await RefundsAsync(elsewherePayment)).Should().BeEmpty();
    }

    // ── Tạm khoá ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task ASuspension_CancelsOnlyTheShowsThatFallInsideIt()
    {
        var venue = await VenueAsync();
        var buyer = await UserAsync();
        var inside = await ShowAsync(venue.LoungeId, DateTimeOffset.UtcNow.AddDays(3));
        var after = await ShowAsync(venue.LoungeId, DateTimeOffset.UtcNow.AddDays(20));
        var (_, insidePayment) = await TicketAsync(inside, buyer);
        var (afterTicket, afterPayment) = await TicketAsync(after, buyer);
        await PenaltyAsync(venue.LoungeId, PenaltyType.Suspension); // 7 ngày

        await ApplyDuePenaltiesAsync();

        (await ShowStatusAsync(inside)).Should().Be(LoungeShowStatus.Cancelled);
        (await RefundsAsync(insidePayment)).Should().ContainSingle().Which.RefundPercentage.Should().Be(100m);
        (await ShowStatusAsync(after)).Should().Be(LoungeShowStatus.Published,
            "the venue may operate again by then — that show can still happen");
        (await TicketStatusAsync(afterTicket)).Should().Be(TicketStatus.Confirmed);
        (await RefundsAsync(afterPayment)).Should().BeEmpty();
        (await NoticesAsync(venue.OwnerId, NotificationType.PenaltyIssued))
            .Should().Contain(n => n.Body.Contains("Đã huỷ 1 buổi diễn rơi vào thời gian tạm khoá"));
    }

    [Fact]
    public async Task APenaltyStillUnderAppeal_CancelsNothingYet()
    {
        var venue = await VenueAsync();
        var buyer = await UserAsync();
        var show = await ShowAsync(venue.LoungeId, DateTimeOffset.UtcNow.AddDays(3));
        var (_, payment) = await TicketAsync(show, buyer);
        await PenaltyAsync(venue.LoungeId, PenaltyType.Ban, PenaltyStatus.Appealed);

        await ApplyDuePenaltiesAsync();

        (await ShowStatusAsync(show)).Should().Be(LoungeShowStatus.Published,
            "an appeal still being decided has not taken effect — the venue may yet be cleared");
        (await RefundsAsync(payment)).Should().BeEmpty();
    }

    // ── Vé tại quầy, vé chuyển nhượng ────────────────────────────────────────

    [Fact]
    public async Task WalkInTickets_GetACashRefundRequest_AndTheOwnerIsToldToHandTheCashBack()
    {
        var venue = await VenueAsync();
        var show = await ShowAsync(venue.LoungeId, DateTimeOffset.UtcNow.AddDays(3));
        var (walkInTicket, walkInPayment) = await TicketAsync(show, payerId: null);
        await PenaltyAsync(venue.LoungeId, PenaltyType.Ban);

        await ApplyDuePenaltiesAsync();

        (await TicketStatusAsync(walkInTicket)).Should().Be(TicketStatus.Cancelled);
        (await RefundsAsync(walkInPayment)).Should().ContainSingle().Which.RefundPercentage.Should().Be(100m,
            "same as when the owner cancels: the venue holds this cash and owes it back");
        (await NoticesAsync(venue.OwnerId, NotificationType.PenaltyIssued))
            .Should().Contain(n => n.Body.Contains("1 vé bán tại quầy") && n.Body.Contains("tiền mặt"),
                "a walk-in buyer has no account — only the venue can reach them");
    }

    [Fact]
    public async Task ATransferredTicket_IsRefundedToWhoPaid_AndBothAreTold()
    {
        var venue = await VenueAsync();
        var payer = await UserAsync();
        var holder = await UserAsync();
        var show = await ShowAsync(venue.LoungeId, DateTimeOffset.UtcNow.AddDays(3));
        var (_, payment) = await TicketAsync(show, payer, holderId: holder);
        await PenaltyAsync(venue.LoungeId, PenaltyType.Ban);

        await ApplyDuePenaltiesAsync();

        (await RefundsAsync(payment)).Should().ContainSingle().Which.RequestedBy.Should().Be(payer,
            "VNPay refunds the original transaction — MLACP-370");
        (await NoticesAsync(payer, NotificationType.EventCancelled)).Should().ContainSingle();
        (await NoticesAsync(holder, NotificationType.EventCancelled))
            .Should().Contain(n => n.Body.Contains("hoàn về người đã mua vé ban đầu"));
    }
}
