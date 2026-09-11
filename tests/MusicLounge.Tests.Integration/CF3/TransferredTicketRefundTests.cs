using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Infrastructure.Jobs;
using MusicLounge.Infrastructure.Persistence;
using MusicLounge.Tests.Integration.Helpers;

namespace MusicLounge.Tests.Integration.CF3;

/// <summary>
/// MLACP-370. VNPay chỉ hoàn về đúng giao dịch gốc, nên tiền hoàn của một vé đã chuyển nhượng về người đã
/// MUA vé. Trước đây mọi đường hoàn do nền tảng ép ghi yêu cầu hoàn dưới tên người đang GIỮ vé và chỉ báo
/// cho họ — người nhận chuyển nhượng tưởng tiền về mình, người mua ban đầu không được báo gì. Người nhận còn
/// tự huỷ được vé, và tiền đi về thẻ của người khác.
///
/// <para>Ticketmaster: "we'd refund the person who purchased the tickets directly from us"; người nhận
/// phải "transfer them back to the original purchaser".</para>
///
/// <para>Mỗi đường hoàn có một bài riêng — cùng một câu hỏi, nhưng mỗi đường là một chỗ gọi riêng có thể
/// bị bỏ sót.</para>
/// </summary>
[Collection("Integration")]
public sealed class TransferredTicketRefundTests
{
    private readonly ApiFactory _factory;

    public TransferredTicketRefundTests(ApiFactory factory) => _factory = factory;

    private sealed record People(int OriginalId, string OriginalEmail, int HolderId);

    private sealed record Seeded(int ShowId, Guid TicketId, int PaymentId, People People);

    private async Task<People> SeedPeopleAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        User NewUser(string tag) => new()
        {
            Email = $"{tag}-{Guid.NewGuid():N}@test.com", FullName = tag, Role = UserRole.Audience,
            AuthProvider = "local", EmailVerifiedAt = DateTimeOffset.UtcNow, IsActive = true
        };
        var original = NewUser("original");
        var holder = NewUser("holder");
        db.Users.AddRange(original, holder);
        await db.SaveChangesAsync();
        return new People(original.Id, original.Email, holder.Id);
    }

    /// <summary>Vé do <c>original</c> trả tiền, đã chuyển nhượng xong cho <c>holder</c>.</summary>
    private async Task<Seeded> SeedTransferredTicketAsync(
        LoungeShow show, AccessType accessType = AccessType.Physical, TicketStatus status = TicketStatus.Confirmed)
    {
        var people = await SeedPeopleAsync();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        db.LoungeShows.Add(show);
        await db.SaveChangesAsync();

        var tier = new TicketTier { LoungeShowId = show.Id, Name = accessType.ToString(), AccessType = accessType, CreatedAt = DateTime.UtcNow };
        db.Add(tier);
        await db.SaveChangesAsync();
        var price = new TicketPrice
        {
            TierId = tier.Id, Name = "Đợt 1", Price = 200_000m, PurchaseChannel = PurchaseChannel.Online,
            SaleStart = DateTimeOffset.UtcNow.AddDays(-30)
        };
        db.Add(price);
        var payment = new Payment
        {
            OrderId = $"MLACP370-{Guid.NewGuid():N}"[..30], PayerId = people.OriginalId,
            GrossAmount = 200_000m, NetAmount = 200_000m, Status = PaymentStatus.Confirmed,
            ReferenceType = "TicketHold", ReferenceId = "0", TransactionId = $"X{Guid.NewGuid():N}"[..16],
            PaidAt = DateTimeOffset.UtcNow.AddDays(-2), CreatedAt = DateTimeOffset.UtcNow.AddDays(-2)
        };
        db.Add(payment);
        await db.SaveChangesAsync();

        var ticket = new Ticket
        {
            Id = Guid.NewGuid(), BuyerId = people.HolderId, PriceId = price.Id, TierId = tier.Id, ShowId = show.Id,
            PaymentId = payment.Id, Status = status, QrCode = $"QR-{Guid.NewGuid():N}",
            PurchaseChannel = PurchaseChannel.Online, CreatedAt = DateTimeOffset.UtcNow.AddDays(-2)
        };
        db.Add(ticket);
        await db.SaveChangesAsync();
        return new Seeded(show.Id, ticket.Id, payment.Id, people);
    }

    private static LoungeShow UpcomingShow(LoungeShowFormat format = LoungeShowFormat.Offline) => new()
    {
        LoungeId = SeedHelper.LoungeId, Name = $"TransferRefund-{Guid.NewGuid():N}"[..28], Description = "test",
        Format = format, Status = LoungeShowStatus.Published, CancellationAllowed = true,
        ScheduledStart = DateTimeOffset.UtcNow.AddDays(20), ScheduledEnd = DateTimeOffset.UtcNow.AddDays(20).AddHours(2),
        VcpmcRoyaltyReference = "VCPMC-TEST"
    };

    private HttpClient Owner => _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner", SeedHelper.LoungeId);
    private HttpClient Admin => _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin");

    private async Task<RefundRequest> RefundForAsync(int paymentId)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>()
            .RefundRequests.AsNoTracking().SingleAsync(r => r.PaymentId == paymentId);
    }

    private async Task<List<Notification>> NoticesToAsync(int userId)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>()
            .Notifications.AsNoTracking().Where(n => n.UserId == userId).ToListAsync();
    }

    private async Task ShouldRefundTheOriginalBuyer_AndTellBothAsync(Seeded seeded)
    {
        (await RefundForAsync(seeded.PaymentId)).RequestedBy.Should().Be(seeded.People.OriginalId,
            "VNPay refunds to the original transaction — the money goes to whoever paid");

        (await NoticesToAsync(seeded.People.OriginalId))
            .Should().Contain(n => n.Title == "Vé bạn đã chuyển nhượng được hoàn tiền",
                "the money lands in the original buyer's account — they must be the one told");
        (await NoticesToAsync(seeded.People.HolderId))
            .Should().Contain(n => n.Body.Contains("người đã mua vé ban đầu"),
                "the holder must not be led to expect money that goes to someone else");
    }

    // ─── Từng đường hoàn do nền tảng ép ───────────────────────────────────────

    [Fact]
    public async Task VenueCancelsTheShow_RefundGoesToTheOriginalBuyer()
    {
        var seeded = await SeedTransferredTicketAsync(UpcomingShow());

        (await Owner.PostAsync($"/api/v1/lounge-shows/{seeded.ShowId}/cancel", null))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        await ShouldRefundTheOriginalBuyer_AndTellBothAsync(seeded);
    }

    [Fact]
    public async Task ShowMovesOnline_PhysicalRefundGoesToTheOriginalBuyer()
    {
        var seeded = await SeedTransferredTicketAsync(UpcomingShow());

        (await Owner.PutAsJsonAsync($"/api/v1/lounge-shows/{seeded.ShowId}/format", new { NewFormat = "Online" }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        await ShouldRefundTheOriginalBuyer_AndTellBothAsync(seeded);
    }

    [Fact]
    public async Task ShowTakenDownOnAReport_RefundGoesToTheOriginalBuyer()
    {
        var seeded = await SeedTransferredTicketAsync(UpcomingShow());
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Add(new ContentReport
            {
                TargetType = ReportTargetType.Show, TargetId = seeded.ShowId, ReporterId = SeedHelper.AudienceId,
                Reason = "Nội dung vi phạm", Status = ContentReportStatus.Open, CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-10)
            });
            await db.SaveChangesAsync();
        }

        (await Admin.PostAsJsonAsync("/api/v1/content-reports/resolve",
                new { TargetType = "Show", TargetId = seeded.ShowId, Action = "Removed", Note = "Gỡ theo báo cáo" }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        await ShouldRefundTheOriginalBuyer_AndTellBothAsync(seeded);
    }

    [Fact]
    public async Task ShowTakenDownOnAComplaint_RefundGoesToTheOriginalBuyer()
    {
        var seeded = await SeedTransferredTicketAsync(UpcomingShow());
        var complaintId = await SeedShowComplaintAsync(seeded.ShowId, SeedHelper.AudienceId);

        (await Admin.PostAsJsonAsync($"/api/v1/complaints/{complaintId}/resolve",
                new { Status = "Resolved", ResolvedAction = "TakeDownContent", Resolution = "Xác nhận vi phạm" }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        await ShouldRefundTheOriginalBuyer_AndTellBothAsync(seeded);
    }

    [Fact]
    public async Task AComplaintRefundForTheHolder_GoesToTheOriginalBuyer_AndTheHolderIsTold()
    {
        var seeded = await SeedTransferredTicketAsync(UpcomingShow());
        var complaintId = await SeedShowComplaintAsync(seeded.ShowId, seeded.People.HolderId);

        (await Admin.PostAsJsonAsync($"/api/v1/complaints/{complaintId}/resolve",
                new { Status = "Resolved", ResolvedAction = "Refund", Resolution = "Chấp nhận khiếu nại" }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        await ShouldRefundTheOriginalBuyer_AndTellBothAsync(seeded);
    }

    [Fact]
    public async Task ALivestreamThatNeverAired_RefundGoesToTheOriginalBuyer()
    {
        var end = DateTimeOffset.UtcNow.AddHours(-12);
        var seeded = await SeedTransferredTicketAsync(new LoungeShow
        {
            LoungeId = SeedHelper.LoungeId, Name = $"NeverAired-{Guid.NewGuid():N}"[..24], Format = LoungeShowFormat.Online,
            Status = LoungeShowStatus.Ended, ScheduledStart = end.AddHours(-2), ScheduledEnd = end, ActualEnd = end,
            CreatedAt = DateTime.UtcNow
        }, AccessType.Livestream);
        await AddLivestreamAsync(seeded.ShowId, LivestreamStatus.Scheduled, startedAt: null, endedAt: null);

        await RunLivestreamRefundJobAsync();

        await ShouldRefundTheOriginalBuyer_AndTellBothAsync(seeded);
    }

    [Fact]
    public async Task ALivestreamCutShort_RefundGoesToTheOriginalBuyer()
    {
        // 30 phút phát trên 100 phút đã bán — dưới ngưỡng 70%.
        var endedAt = DateTimeOffset.UtcNow.AddHours(-2);
        var startedAt = endedAt.AddMinutes(-30);
        var seeded = await SeedTransferredTicketAsync(new LoungeShow
        {
            LoungeId = SeedHelper.LoungeId, Name = $"CutShort370-{Guid.NewGuid():N}"[..24], Format = LoungeShowFormat.Online,
            Status = LoungeShowStatus.Ended, ScheduledStart = startedAt, ScheduledEnd = startedAt.AddMinutes(100),
            ActualStart = startedAt, ActualEnd = endedAt, RatingOpenUntil = DateTimeOffset.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow
        }, AccessType.Livestream);
        await AddLivestreamAsync(seeded.ShowId, LivestreamStatus.Ended, startedAt, endedAt);

        await RunLivestreamRefundJobAsync();

        await ShouldRefundTheOriginalBuyer_AndTellBothAsync(seeded);
    }

    // ─── Người nhận chuyển nhượng ─────────────────────────────────────────────

    [Fact]
    public async Task TheHolder_CannotCancelForMoneyThatGoesToSomeoneElse_ButCanHandTheTicketBack()
    {
        var seeded = await SeedTransferredTicketAsync(UpcomingShow());
        var holder = _factory.CreateAuthenticatedClient(seeded.People.HolderId, "Audience");
        var original = _factory.CreateAuthenticatedClient(seeded.People.OriginalId, "Audience");

        var refused = await holder.PostAsync($"/api/v1/tickets/{seeded.TicketId}/cancel", null);
        refused.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await refused.Content.ReadAsStringAsync()).Should().Contain("chuyển vé lại cho người mua ban đầu");

        (await holder.PostAsJsonAsync($"/api/v1/tickets/{seeded.TicketId}/transfer",
                new { RecipientEmail = seeded.People.OriginalEmail }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await original.PostAsync($"/api/v1/tickets/{seeded.TicketId}/transfer/accept", null))
            .IsSuccessStatusCode.Should().BeTrue();

        (await original.PostAsync($"/api/v1/tickets/{seeded.TicketId}/cancel", null))
            .IsSuccessStatusCode.Should().BeTrue("back in the original buyer's hands, the normal policy applies");
        (await RefundForAsync(seeded.PaymentId)).RequestedBy.Should().Be(seeded.People.OriginalId);
    }

    [Fact]
    public async Task ATransferInvitation_SaysWhereRefundsGo()
    {
        var seeded = await SeedTransferredTicketAsync(UpcomingShow());
        var holder = _factory.CreateAuthenticatedClient(seeded.People.HolderId, "Audience");

        (await holder.PostAsJsonAsync($"/api/v1/tickets/{seeded.TicketId}/transfer",
                new { RecipientEmail = seeded.People.OriginalEmail }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await NoticesToAsync(seeded.People.OriginalId))
            .Should().Contain(n => n.ReferenceId == seeded.TicketId.ToString() && n.Body.Contains("người đã mua vé ban đầu"),
                "someone accepting a ticket should know, before they accept, that refunds never reach them");
    }

    // ─── Dựng dữ liệu phụ ─────────────────────────────────────────────────────

    private async Task<int> SeedShowComplaintAsync(int showId, int complainantId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var complaint = new Complaint
        {
            ComplainantUserId = complainantId, TargetType = "show", TargetId = showId,
            Category = ComplaintCategory.EventMisrepresentation, Description = "Buổi diễn không đúng như quảng cáo",
            Status = ComplaintStatus.Open, CreatedAt = DateTimeOffset.UtcNow.AddHours(-1)
        };
        db.Set<Complaint>().Add(complaint);
        await db.SaveChangesAsync();
        return complaint.Id;
    }

    private async Task AddLivestreamAsync(int showId, LivestreamStatus status, DateTimeOffset? startedAt, DateTimeOffset? endedAt)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Add(new Livestream
        {
            LoungeShowId = showId, Status = status, StartedAt = startedAt, EndedAt = endedAt, CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }

    private async Task RunLivestreamRefundJobAsync()
    {
        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<RefundUndeliveredLivestreamTicketsJob>()
            .ExecuteAsync(new JobCancellationToken(false));
    }
}
