using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.ValueObjects;
using MusicLounge.Infrastructure.Persistence;
using MusicLounge.Tests.Integration.Helpers;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Tests.Integration.LoungeShows;

/// <summary>
/// MLACP-372. Đổi lịch (và đổi địa chỉ, chép cùng khuôn) từng bật <c>CancellationAllowed</c> VĨNH VIỄN và xoá hạn huỷ
/// của phòng trà: người mua <b>sau</b> — đã thấy ngày / địa chỉ mới trước khi trả tiền — cũng huỷ được, còn người mua
/// <b>trước</b> bị hoàn theo tỉ lệ của phòng trà, tức bị trừ tiền vì chính phòng trà đổi.
///
/// <para>Nay (đã chốt): người mua trước thay đổi được huỷ và hoàn <b>100%</b> tới hạn huỷ của buổi diễn tính theo lịch
/// mới (không có hạn, hoặc hạn đã qua lúc đổi, thì tới giờ bắt đầu); người mua sau theo chính sách phòng trà. Đổi địa
/// chỉ chỉ tính cho vé vào cửa.</para>
///
/// <para>Mỗi bài dùng phòng trà riêng — lịch mới luôn trống, và đổi địa chỉ không đụng dữ liệu seed dùng chung. Mốc
/// đổi lịch đã qua được ghi qua <c>Entry(...).Property</c>, và mốc hoàn đủ trên chi tiết vé được đọc từ JSON — để file
/// vẫn biên dịch được trên code cũ và bước "đỏ trước khi sửa" chạy được.</para>
/// </summary>
[Collection("Integration")]
public sealed class ChangedAfterSaleRefundTests
{
    private const string OldStreet = "10 Đồng Khởi";
    private const string NewStreet = "88 Nguyễn Huệ";

    private readonly ApiFactory _factory;

    public ChangedAfterSaleRefundTests(ApiFactory factory) => _factory = factory;

    private sealed record Venue(int LoungeId, string Name, int OwnerId);

    private static DateTimeOffset Earlier => DateTimeOffset.UtcNow.AddDays(-1);

    private async Task<Venue> VenueAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var freshOwner = new User { Email = $"v377-{Guid.NewGuid():N}@test.com", FullName = "Test Venue Owner" };
        db.Users.Add(freshOwner);
        await db.SaveChangesAsync();

        var lounge = new MusicLoungeEntity
        {
            OwnerId = freshOwner.Id,
            Name = $"Phòng trà {Guid.NewGuid():N}"[..20],
            Status = LoungeStatus.Approved,
            Address = new VenueAddress
            {
                Street = OldStreet, Ward = "Bến Nghé", District = "1", City = "HCM",
                Latitude = 10.7769, Longitude = 106.7009
            }
        };
        db.Add(lounge);
        await db.SaveChangesAsync();
        return new Venue(lounge.Id, lounge.Name, freshOwner.Id);
    }

    private async Task<int> UserAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = new User { Email = $"changed372-{Guid.NewGuid():N}@test.com", FullName = "Người mua" };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }

    private async Task<int> ShowAsync(
        Venue venue, DateTimeOffset start, bool cancellationAllowed, decimal? refundPercentage = null,
        int? deadlineHours = null, LoungeShowFormat format = LoungeShowFormat.Offline,
        DateTimeOffset? rescheduledAt = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var show = new LoungeShow
        {
            LoungeId = venue.LoungeId,
            Name = $"Đêm nhạc {Guid.NewGuid():N}"[..18],
            Description = "MLACP-372",
            Format = format,
            Status = LoungeShowStatus.Published,
            ScheduledStart = start,
            ScheduledEnd = start.AddHours(3),
            CancellationAllowed = cancellationAllowed,
            RefundPercentage = refundPercentage,
            CancellationDeadlineHours = deadlineHours,
            CreatedAt = DateTime.UtcNow
        };
        db.Add(show);
        if (rescheduledAt is not null)
            db.Entry(show).Property<DateTimeOffset?>("RescheduledAt").CurrentValue = rescheduledAt;
        await db.SaveChangesAsync();
        return show.Id;
    }

    private async Task<(Guid TicketId, int PaymentId)> TicketAsync(
        int showId, int holderId, DateTimeOffset boughtAt, int? payerId = null,
        AccessType access = AccessType.Physical)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var tier = new TicketTier
        {
            LoungeShowId = showId, Name = access.ToString(), AccessType = access, CreatedAt = DateTime.UtcNow
        };
        db.Add(tier);
        await db.SaveChangesAsync();

        var price = new TicketPrice
        {
            TierId = tier.Id, Name = "Đợt 1", Price = 200_000m,
            PurchaseChannel = PurchaseChannel.Online, SaleStart = DateTimeOffset.UtcNow.AddDays(-30)
        };
        db.Add(price);
        await db.SaveChangesAsync();

        var payment = new Payment
        {
            OrderId = $"MLACP372-{Guid.NewGuid():N}"[..30],
            PayerId = payerId ?? holderId,
            GrossAmount = 200_000m,
            NetAmount = 200_000m,
            Status = PaymentStatus.Confirmed,
            ReferenceType = "TicketHold",
            ReferenceId = "0",
            TransactionId = $"V{Guid.NewGuid():N}"[..16],
            PaidAt = boughtAt,
            CreatedAt = boughtAt
        };
        db.Add(payment);
        await db.SaveChangesAsync();

        var ticket = new Ticket
        {
            Id = Guid.NewGuid(),
            BuyerId = holderId,
            PriceId = price.Id,
            TierId = tier.Id,
            ShowId = showId,
            PaymentId = payment.Id,
            Status = TicketStatus.Confirmed,
            PurchaseChannel = PurchaseChannel.Online,
            CreatedAt = boughtAt
        };
        db.Add(ticket);
        await db.SaveChangesAsync();
        return (ticket.Id, payment.Id);
    }

    private Task<HttpResponseMessage> RescheduleAsync(Venue venue, int showId, DateTimeOffset newStart)
        => _factory.CreateAuthenticatedClient(venue.OwnerId, "Owner")
            .PostAsJsonAsync($"/api/v1/lounge-shows/{showId}/reschedule", new { NewScheduledStart = newStart });

    private Task<HttpResponseMessage> MoveVenueAsync(Venue venue)
        => _factory.CreateAuthenticatedClient(venue.OwnerId, "Owner")
            .PutAsJsonAsync($"/api/v1/lounges/{venue.LoungeId}", new
            {
                Name = venue.Name,
                Description = (string?)null,
                AtmosphereId = (int?)null,
                Street = NewStreet,
                Ward = "Bến Nghé",
                District = "1",
                City = "HCM",
                Latitude = 10.7769,
                Longitude = 106.7009
            });

    private Task<HttpResponseMessage> CancelAsync(Guid ticketId, int userId)
        => _factory.CreateAuthenticatedClient(userId, "Audience").PostAsync($"/api/v1/tickets/{ticketId}/cancel", null);

    private async Task<RefundRequest> RefundAsync(int paymentId)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().RefundRequests.AsNoTracking()
            .SingleAsync(r => r.PaymentId == paymentId);
    }

    private async Task<string> NoticeAsync(int userId, NotificationType type, int showId)
    {
        using var scope = _factory.Services.CreateScope();
        return (await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Notifications.AsNoTracking()
                .Where(n => n.UserId == userId && n.Type == type && n.ReferenceId == showId.ToString())
                .ToListAsync())
            .Should().ContainSingle().Subject.Body;
    }

    private async Task<DateTimeOffset?> FullRefundUntilAsync(Guid ticketId)
    {
        var res = await _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience")
            .GetAsync($"/api/v1/tickets/{ticketId}");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("data").TryGetProperty("fullRefundUntil", out var until)
               && until.ValueKind != JsonValueKind.Null
            ? until.GetDateTimeOffset()
            : null;
    }

    private static string Vn(DateTimeOffset at)
        => at.ToOffset(TimeSpan.FromHours(7)).ToString("HH:mm dd/MM/yyyy", CultureInfo.InvariantCulture);

    // ── Đổi lịch: người mua trước ────────────────────────────────────────────

    [Fact]
    public async Task EarlierBuyer_AfterAReschedule_IsRefundedInFull_NotAtTheVenuesRate()
    {
        var venue = await VenueAsync();
        var showId = await ShowAsync(venue, DateTimeOffset.UtcNow.AddDays(30), cancellationAllowed: true,
            refundPercentage: 70m);
        var (ticketId, paymentId) = await TicketAsync(showId, SeedHelper.AudienceId, Earlier);

        (await RescheduleAsync(venue, showId, DateTimeOffset.UtcNow.AddDays(20))).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await CancelAsync(ticketId, SeedHelper.AudienceId)).IsSuccessStatusCode.Should().BeTrue();
        (await RefundAsync(paymentId)).RefundPercentage.Should().Be(100m,
            "the venue moved the date after this buyer paid — they must not lose 30% for it");
    }

    [Fact]
    public async Task EarlierBuyer_CanCancel_EvenWhenTheShowWasSoldAsNonRefundable()
    {
        var venue = await VenueAsync();
        var showId = await ShowAsync(venue, DateTimeOffset.UtcNow.AddDays(30), cancellationAllowed: false);
        var (ticketId, paymentId) = await TicketAsync(showId, SeedHelper.AudienceId, Earlier);

        (await RescheduleAsync(venue, showId, DateTimeOffset.UtcNow.AddDays(20))).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await CancelAsync(ticketId, SeedHelper.AudienceId)).IsSuccessStatusCode.Should().BeTrue(
            "'no self-cancellation' was agreed for the old date, not for one the venue picked afterwards");
        (await RefundAsync(paymentId)).RefundPercentage.Should().Be(100m);
    }

    [Fact]
    public async Task EarlierBuyer_WhoseDeadlineWasAlreadyPastAtTheReschedule_CanCancelUntilTheStart()
    {
        var venue = await VenueAsync();
        var showId = await ShowAsync(venue, DateTimeOffset.UtcNow.AddDays(30), cancellationAllowed: true,
            refundPercentage: 70m, deadlineHours: 24 * 14);
        var (ticketId, paymentId) = await TicketAsync(showId, SeedHelper.AudienceId, Earlier);

        // 12 ngày tới: đủ 7 ngày làm việc để được đổi lịch, nhưng hạn huỷ 14 ngày trước giờ diễn đã qua ngay lúc đổi.
        (await RescheduleAsync(venue, showId, DateTimeOffset.UtcNow.AddDays(12))).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await CancelAsync(ticketId, SeedHelper.AudienceId)).IsSuccessStatusCode.Should().BeTrue(
            "a window that is shut the moment it opens is no window");
        (await RefundAsync(paymentId)).RefundPercentage.Should().Be(100m);
    }

    [Fact]
    public async Task EarlierBuyer_WindowEndsAtTheShowsOwnDeadlineOnTheNewDate()
    {
        // Đổi lịch 10 ngày trước sang một ngày nay chỉ còn 24 giờ; hạn huỷ 48 giờ trước giờ diễn vẫn đạt được lúc đổi,
        // nên cửa sổ 100% đóng ở hạn đó — đã qua 24 giờ. Hết cửa sổ thì về chính sách thường, cũng đã quá hạn.
        var venue = await VenueAsync();
        var showId = await ShowAsync(venue, DateTimeOffset.UtcNow.AddHours(24), cancellationAllowed: true,
            refundPercentage: 70m, deadlineHours: 48, rescheduledAt: DateTimeOffset.UtcNow.AddDays(-10));
        var (ticketId, _) = await TicketAsync(showId, SeedHelper.AudienceId, DateTimeOffset.UtcNow.AddDays(-11));

        (await CancelAsync(ticketId, SeedHelper.AudienceId)).StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "the reopened right runs to the show's own deadline on the new date, not beyond it");
    }

    // ── Đổi lịch: người mua sau ──────────────────────────────────────────────

    [Fact]
    public async Task LaterBuyer_OfANonRefundableShow_StillCannotCancel_TheRescheduleDidNotReopenItForGood()
    {
        var venue = await VenueAsync();
        var showId = await ShowAsync(venue, DateTimeOffset.UtcNow.AddDays(30), cancellationAllowed: false);
        (await RescheduleAsync(venue, showId, DateTimeOffset.UtcNow.AddDays(20))).StatusCode.Should().Be(HttpStatusCode.NoContent);
        var (ticketId, _) = await TicketAsync(showId, SeedHelper.AudienceId, DateTimeOffset.UtcNow);

        (await CancelAsync(ticketId, SeedHelper.AudienceId)).StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "this buyer saw the new date and the no-cancellation policy before paying");

        using var scope = _factory.Services.CreateScope();
        (await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().LoungeShows.AsNoTracking()
                .SingleAsync(s => s.Id == showId))
            .CancellationAllowed.Should().BeFalse("the show page keeps advertising the policy the owner chose");
    }

    [Fact]
    public async Task LaterBuyer_IsRefundedAtTheVenuesRate()
    {
        var venue = await VenueAsync();
        var showId = await ShowAsync(venue, DateTimeOffset.UtcNow.AddDays(30), cancellationAllowed: true,
            refundPercentage: 70m);
        (await RescheduleAsync(venue, showId, DateTimeOffset.UtcNow.AddDays(20))).StatusCode.Should().Be(HttpStatusCode.NoContent);
        var (ticketId, paymentId) = await TicketAsync(showId, SeedHelper.AudienceId, DateTimeOffset.UtcNow);

        (await CancelAsync(ticketId, SeedHelper.AudienceId)).IsSuccessStatusCode.Should().BeTrue();
        (await RefundAsync(paymentId)).RefundPercentage.Should().Be(70m, "the venue's own policy, which this buyer saw");
    }

    // ── Đổi địa chỉ ──────────────────────────────────────────────────────────

    [Fact]
    public async Task AddressChange_OnAHybridShow_GivesTheFullRefundRightToVenueTicketsOnly()
    {
        var venue = await VenueAsync();
        var showId = await ShowAsync(venue, DateTimeOffset.UtcNow.AddDays(30), cancellationAllowed: false,
            format: LoungeShowFormat.Hybrid);
        var (venueTicket, venuePayment) = await TicketAsync(showId, SeedHelper.AudienceId, Earlier);
        var viewer = await UserAsync();
        var (streamTicket, _) = await TicketAsync(showId, viewer, Earlier, access: AccessType.Livestream);

        (await MoveVenueAsync(venue)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await CancelAsync(streamTicket, viewer)).StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "a livestream viewer never had to travel — the new address changes nothing they bought");
        (await CancelAsync(venueTicket, SeedHelper.AudienceId)).IsSuccessStatusCode.Should().BeTrue();
        (await RefundAsync(venuePayment)).RefundPercentage.Should().Be(100m);
    }

    // ── Thông báo ────────────────────────────────────────────────────────────

    [Fact]
    public async Task RescheduleNotice_StatesTheWindow_AndTellsATransferredHolderToHandTheTicketBack()
    {
        var venue = await VenueAsync();
        var showId = await ShowAsync(venue, DateTimeOffset.UtcNow.AddDays(30), cancellationAllowed: true,
            refundPercentage: 70m);
        var originalBuyer = await UserAsync();
        await TicketAsync(showId, SeedHelper.AudienceId, Earlier, payerId: originalBuyer);
        var newStart = DateTimeOffset.UtcNow.AddDays(20);

        (await RescheduleAsync(venue, showId, newStart)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var body = await NoticeAsync(SeedHelper.AudienceId, NotificationType.EventRescheduled, showId);
        body.Should().Contain($"hoàn 100% tiền vé tới {Vn(newStart)}",
            "the holder is told the exact window CancelTicket applies");
        body.Should().Contain("chuyển vé lại",
            "since MLACP-370 only the original buyer can cancel a transferred ticket");
    }

    [Fact]
    public async Task AddressChangeNotice_StatesTheWindow_AndTellsATransferredHolderToHandTheTicketBack()
    {
        var venue = await VenueAsync();
        var start = DateTimeOffset.UtcNow.AddDays(30);
        var showId = await ShowAsync(venue, start, cancellationAllowed: true, refundPercentage: 70m);
        var originalBuyer = await UserAsync();
        await TicketAsync(showId, SeedHelper.AudienceId, Earlier, payerId: originalBuyer);

        (await MoveVenueAsync(venue)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var body = await NoticeAsync(SeedHelper.AudienceId, NotificationType.EventVenueChanged, showId);
        body.Should().Contain($"hoàn 100% tiền vé tới {Vn(start)}");
        body.Should().Contain("chuyển vé lại");
    }

    // ── Chi tiết vé: trang buổi diễn nói theo chính sách chung, chỉ vé mới biết quyền riêng ──

    [Fact]
    public async Task TicketDetail_ShowsTheFullRefundWindow_ToEarlierBuyersOnly()
    {
        var venue = await VenueAsync();
        var showId = await ShowAsync(venue, DateTimeOffset.UtcNow.AddDays(30), cancellationAllowed: false);
        var (earlier, _) = await TicketAsync(showId, SeedHelper.AudienceId, Earlier);
        var newStart = DateTimeOffset.UtcNow.AddDays(20);
        (await RescheduleAsync(venue, showId, newStart)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        var (later, _) = await TicketAsync(showId, SeedHelper.AudienceId, DateTimeOffset.UtcNow);

        var earlierUntil = await FullRefundUntilAsync(earlier);
        earlierUntil.Should().NotBeNull(
            "the show page says 'no cancellation' — only the ticket can tell this buyer otherwise");
        earlierUntil!.Value.Should().BeCloseTo(newStart, TimeSpan.FromSeconds(1));
        (await FullRefundUntilAsync(later)).Should().BeNull();
    }
}
