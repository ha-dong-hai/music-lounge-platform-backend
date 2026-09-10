using System.Globalization;
using System.Net;
using System.Net.Http.Json;
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
/// MLACP-358. Hệ thống tính mốc thời gian theo UTC, còn chuỗi định dạng của DateTimeOffset in giờ
/// theo chính offset của giá trị — nên các thông báo in thẳng mốc thời gian đã nói với người đọc
/// một giờ chậm 7 tiếng: chủ phòng trà đọc "phạt có hiệu lực từ 14:00" trong khi thật ra là 21:00,
/// và tính sai thời gian còn lại để kháng cáo.
///
/// <para>Mỗi bài kiểm một chỗ in giờ, qua đúng đường thật (API hoặc job), với mốc thời gian mang
/// offset +00:00 — đúng cái hệ thống tự sinh ra, và cũng là cái frontend gửi lên khi dùng
/// <c>toISOString()</c>. Giá trị mong đợi được tính độc lập ở đây, không gọi lại helper đang được
/// kiểm, để một helper sai không tự xác nhận chính nó.</para>
/// </summary>
[Collection("Integration")]
public sealed class VietnamTimeInMessagesTests
{
    private const string Full = "dd/MM/yyyy HH:mm";
    private const string TimeFirst = "HH:mm dd/MM/yyyy";
    private const string DayOnly = "dd/MM/yyyy";

    private readonly ApiFactory _factory;

    public VietnamTimeInMessagesTests(ApiFactory factory) => _factory = factory;

    private static string Vn(DateTimeOffset value, string format)
        => value.ToOffset(TimeSpan.FromHours(7)).ToString(format, CultureInfo.InvariantCulture);

    private static string Utc(DateTimeOffset value, string format)
        => value.ToUniversalTime().ToString(format, CultureInfo.InvariantCulture);

    private static void ShouldSayVietnamTime(string text, DateTimeOffset moment, string format)
    {
        text.Should().Contain(Vn(moment, format), "người đọc ở Việt Nam, giờ in ra phải là giờ Việt Nam");
        text.Should().NotContain(Utc(moment, format), "giờ UTC chậm 7 tiếng so với đồng hồ người đọc");
    }

    private async Task<(int OwnerId, int LoungeId)> FreshOwnerAndLoungeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var owner = new User { Email = $"vntime-{Guid.NewGuid():N}@test.com", FullName = "VN Time Owner" };
        db.Users.Add(owner);
        await db.SaveChangesAsync();

        var lounge = new MusicLoungeVenue
        {
            OwnerId = owner.Id, Name = $"VnTimeLounge-{Guid.NewGuid():N}"[..30],
            Status = LoungeStatus.Approved,
            Address = new VenueAddress { Street = "1 Test St", District = "1", City = "HCM" }
        };
        db.Lounges.Add(lounge);
        await db.SaveChangesAsync();

        return (owner.Id, lounge.Id);
    }

    private async Task<int> SeedPublishedShowWithBuyerAsync(DateTimeOffset start)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var show = new LoungeShow
        {
            LoungeId = SeedHelper.LoungeId, Name = $"VnTimeShow-{Guid.NewGuid():N}",
            Description = "test", Format = LoungeShowFormat.Offline, Status = LoungeShowStatus.Published,
            ScheduledStart = start, ScheduledEnd = start.AddHours(2), VcpmcRoyaltyReference = "VCPMC-TEST"
        };
        db.Add(show);
        await db.SaveChangesAsync();

        db.Add(new Ticket
        {
            Id = Guid.NewGuid(), BuyerId = SeedHelper.AudienceId, PriceId = SeedHelper.TicketPriceId,
            TierId = SeedHelper.TicketTierId, ShowId = show.Id, Status = TicketStatus.Confirmed,
            PurchaseChannel = PurchaseChannel.Online, CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        return show.Id;
    }

    // ─── Chủ phòng trà: phạt và kháng cáo ─────────────────────────────────────

    [Theory]
    [InlineData("Suspension", 5)]
    [InlineData("Ban", null)]
    public async Task PenaltyNotice_SaysWhenItTakesEffect_InVietnamTime(string penaltyType, int? suspensionDays)
    {
        var (ownerId, loungeId) = await FreshOwnerAndLoungeAsync();
        var admin = _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin");

        var res = await admin.PostAsJsonAsync("/api/v1/venue-penalties", new
        {
            LoungeId = loungeId, PenaltyType = penaltyType, Reason = "Vi phạm lặp lại",
            EvidenceRef = (string?)null, SuspensionDays = suspensionDays
        });
        res.StatusCode.Should().Be(HttpStatusCode.Created);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var penalty = await db.VenuePenalties.SingleAsync(p => p.LoungeId == loungeId);
        var notice = await db.Notifications.SingleAsync(n =>
            n.UserId == ownerId && n.Type == NotificationType.PenaltyIssued
            && n.ReferenceId == penalty.Id.ToString());

        ShouldSayVietnamTime(notice.Body, penalty.EffectiveAt, Full);
    }

    [Fact]
    public async Task AppealNotice_TellsAdminTheDeadline_InVietnamTime()
    {
        var (ownerId, loungeId) = await FreshOwnerAndLoungeAsync();
        int penaltyId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var penalty = new VenuePenalty
            {
                LoungeId = loungeId, PenaltyType = PenaltyType.Suspension, Reason = "Test violation",
                IssuedBy = SeedHelper.AdminId, IssuedAt = DateTimeOffset.UtcNow,
                EffectiveAt = DateTimeOffset.UtcNow.AddHours(24), SuspensionDays = 5,
                Status = PenaltyStatus.Active
            };
            db.VenuePenalties.Add(penalty);
            await db.SaveChangesAsync();
            penaltyId = penalty.Id;
        }

        var owner = _factory.CreateAuthenticatedClient(ownerId, "Owner", loungeId);
        var res = await owner.PostAsJsonAsync(
            $"/api/v1/venue-penalties/{penaltyId}/appeal", new { AppealReason = "Chúng tôi không vi phạm" });
        res.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var verify = _factory.Services.CreateScope();
        var vdb = verify.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var appealed = await vdb.VenuePenalties.SingleAsync(p => p.Id == penaltyId);
        var notice = await vdb.Notifications.SingleAsync(n =>
            n.UserId == SeedHelper.AdminId && n.Type == NotificationType.PenaltyIssued
            && n.ReferenceId == penaltyId.ToString());

        ShouldSayVietnamTime(notice.Body, appealed.AppealDeadline!.Value, Full);
    }

    [Fact]
    public async Task SubscriptionWarning_SaysTheExpiryDate_InVietnamTime()
    {
        // Mốc hết hạn rơi vào buổi tối giờ UTC (từ 17:30) — lúc đó ở Việt Nam đã sang ngày hôm sau,
        // nên ngày in theo UTC và ngày in theo giờ Việt Nam khác nhau. Còn 6–7 ngày → mốc cảnh báo 7.
        var t = DateTimeOffset.UtcNow.AddDays(6.05);
        var expiresAt = t.UtcDateTime.TimeOfDay >= TimeSpan.FromHours(17.5)
            ? t
            : new DateTimeOffset(t.UtcDateTime.Date.AddHours(17.5), TimeSpan.Zero);

        int ownerId, subId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var owner = new User { Email = $"vntime-sub-{Guid.NewGuid():N}@test.com", FullName = "VN Time Sub" };
            db.Users.Add(owner);
            var package = new SubscriptionPackage
            {
                Name = $"VnTimePkg-{Guid.NewGuid():N}"[..20], Price = 200_000m,
                BillingCycle = SubscriptionBillingCycle.Monthly, MaxTicketsPerEvent = 50,
                HasAiPoster = false, IsActive = true
            };
            db.Add(package);
            await db.SaveChangesAsync();

            var sub = new OwnerSubscription
            {
                OwnerId = owner.Id, PackageId = package.Id,
                StartedAt = DateTimeOffset.UtcNow.AddDays(-23), ExpiresAt = expiresAt,
                Status = SubscriptionStatus.Active, MaxTicketsPerEventSnapshot = 50, HasAiPosterSnapshot = false
            };
            db.Add(sub);
            await db.SaveChangesAsync();
            ownerId = owner.Id;
            subId = sub.Id;
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var job = scope.ServiceProvider.GetRequiredService<SubscriptionExpiryWarningJob>();
            await job.ExecuteAsync(new JobCancellationToken(false));
        }

        using var verify = _factory.Services.CreateScope();
        var vdb = verify.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var notice = await vdb.Notifications.SingleAsync(n =>
            n.UserId == ownerId && n.Type == NotificationType.SubscriptionExpiring
            && n.ReferenceId == $"{subId}:7");

        ShouldSayVietnamTime(notice.Body, expiresAt, DayOnly);
    }

    [Fact]
    public async Task UnreachableCancellationDeadline_Error_StatesTheStart_InVietnamTime()
    {
        // Hạn huỷ 24 giờ cho một buổi diễn còn 10 giờ nữa là đã trôi qua — lỗi nêu lại giờ diễn.
        var start = DateTimeOffset.UtcNow.AddHours(10);
        var owner = _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner");

        var res = await owner.PostAsJsonAsync("/api/v1/lounge-shows", new
        {
            LoungeId = SeedHelper.LoungeId, Name = $"VnTimePolicy-{Guid.NewGuid():N}",
            Description = "Integration test show", Format = "Offline",
            ScheduledStart = start, ScheduledEnd = start.AddHours(3),
            TicketSaleClosesAt = (DateTimeOffset?)null, CategoryId = (int?)null,
            OfflineQuota = (int?)null, OnlineQuota = (int?)null,
            GenreIds = Array.Empty<int>(), MoodIds = Array.Empty<int>(), AtmosphereIds = Array.Empty<int>(),
            Performances = Array.Empty<object>(),
            CancellationAllowed = (bool?)true, RefundPercentage = (decimal?)null, CancellationDeadlineHours = (int?)24
        });

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await res.Content.ReadAsStringAsync();
        ShouldSayVietnamTime(body, start, Full);
        body.Should().NotContain("UTC", "người đọc là chủ phòng trà, không phải người vận hành máy chủ");
    }

    [Fact]
    public async Task RefundTooOldForVnPay_Error_StatesThePaymentDate_InVietnamTime()
    {
        // 20:00 UTC là 03:00 sáng hôm sau ở Việt Nam — ngày thanh toán in theo hai múi giờ khác nhau.
        var paidAt = new DateTimeOffset(DateTime.UtcNow.Date.AddDays(-100).AddHours(20), TimeSpan.Zero);
        int refundId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var payment = new Payment
            {
                OrderId = $"VNT-{Guid.NewGuid():N}"[..30], GrossAmount = 100_000m,
                Status = PaymentStatus.Confirmed, ReferenceType = "TicketHold", ReferenceId = "0",
                PaidAt = paidAt, CreatedAt = paidAt
            };
            db.Add(payment);
            await db.SaveChangesAsync();

            var refund = new RefundRequest
            {
                PaymentId = payment.Id, RequestedBy = SeedHelper.AudienceId, Reason = "test",
                AmountRequested = 100_000m, RefundPercentage = 100m, Status = RefundRequestStatus.Pending
            };
            db.Add(refund);
            await db.SaveChangesAsync();
            refundId = refund.Id;
        }

        var admin = _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin");
        var res = await admin.PostAsJsonAsync(
            $"/api/v1/admin/refund-requests/{refundId}/process", new { Decision = "Approved" });

        res.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        ShouldSayVietnamTime(await res.Content.ReadAsStringAsync(), paidAt, DayOnly);
    }

    // ─── Người mua: đổi lịch và nhắc lịch ─────────────────────────────────────

    [Fact]
    public async Task RescheduleNotice_StatesOldAndNewStart_InVietnamTime()
    {
        var oldStart = SeedHelper.NextShowStart().ToUniversalTime();
        var showId = await SeedPublishedShowWithBuyerAsync(oldStart);
        var newStart = SeedHelper.NextShowStart().ToUniversalTime();

        var owner = _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner", SeedHelper.LoungeId);
        var res = await owner.PostAsJsonAsync(
            $"/api/v1/lounge-shows/{showId}/reschedule", new { NewScheduledStart = newStart });
        res.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var notice = await db.Notifications.SingleAsync(n =>
            n.UserId == SeedHelper.AudienceId && n.Type == NotificationType.EventRescheduled
            && n.ReferenceId == showId.ToString());

        ShouldSayVietnamTime(notice.Body, oldStart, TimeFirst);
        ShouldSayVietnamTime(notice.Body, newStart, TimeFirst);
    }

    [Fact]
    public async Task ShowReminder_StatesTheStart_InVietnamTime()
    {
        var start = DateTimeOffset.UtcNow.AddHours(12); // trong cửa sổ nhắc mặc định 24 giờ
        var showId = await SeedPublishedShowWithBuyerAsync(start);

        using (var scope = _factory.Services.CreateScope())
        {
            var job = scope.ServiceProvider.GetRequiredService<EventReminderJob>();
            await job.ExecuteAsync(new JobCancellationToken(false));
        }

        using var verify = _factory.Services.CreateScope();
        var db = verify.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var notice = await db.Notifications.SingleAsync(n =>
            n.UserId == SeedHelper.AudienceId && n.Type == NotificationType.EventReminder
            && n.ReferenceId == showId.ToString());

        ShouldSayVietnamTime(notice.Body, start, TimeFirst);
    }
}
