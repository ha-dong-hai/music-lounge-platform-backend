using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.ValueObjects;
using MusicLounge.Infrastructure.Jobs;
using MusicLounge.Infrastructure.Persistence;
using MusicLounge.Tests.Integration.Helpers;
using MusicLoungeVenue = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Tests.Integration.CF4;

/// <summary>
/// MLACP-362. Hạn phòng trà phải chuyển tiền donate cho nghệ sĩ từng được tính theo năm cách khác
/// nhau, và cách của job nhắc/cảnh cáo (tính từ lúc chủ bấm "đã nhận") còn thưởng cho sự chậm trễ: tự
/// xác nhận đặt lại mốc đó, nên chủ im lặng bị cảnh cáo muộn hơn chủ xác nhận ngay. Nay mọi chỗ tính từ
/// cùng một mốc — lúc phòng trà thật sự nhận tiền (nền tảng giải ngân chặng 1).
///
/// <para>Mỗi bài dựng phòng trà riêng, tạo donate qua đúng API + IPN, rồi đặt thời điểm giải ngân và
/// trạng thái trực tiếp để mô phỏng thời gian trôi — không thể chờ 7 ngày trong một bài test.</para>
/// </summary>
[Collection("Integration")]
public sealed class DonationPayoutDeadlineTests
{
    private const decimal Amount = 100_000m;

    private readonly ApiFactory _factory;

    public DonationPayoutDeadlineTests(ApiFactory factory) => _factory = factory;

    private sealed record Venue(int OwnerId, int LoungeId, int PerformanceId);

    private sealed record InitData(int DonationId, string OrderId);

    private sealed record Wrapped<T>(T Data);

    private sealed record Page<T>(List<T> Items);

    private sealed record PendingItem(
        int Id, DateTimeOffset? PaymentConfirmedAt, DateTimeOffset? AutoConfirmDeadline,
        DateTimeOffset? PayoutReceivedAt, DateTimeOffset? PayoutDueAt);

    private sealed record HistoryItem(int Id, string PayoutStatus, DateTimeOffset? PayoutDueAt);

    private sealed record History(Page<HistoryItem> Items);

    private static DateTimeOffset DaysAgo(double days) => DateTimeOffset.UtcNow.AddDays(-days);

    private async Task<int> HoldDaysAsync()
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ISystemConfigService>()
            .GetIntAsync(ConfigKeys.DonationHoldDays, 7, CancellationToken.None);
    }

    private async Task<Venue> SeedVenueAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var pii = scope.ServiceProvider.GetRequiredService<IPiiEncryptionService>();

        var owner = new User { Email = $"deadline-owner-{Guid.NewGuid():N}@test.com", FullName = "Deadline Owner" };
        db.Users.Add(owner);
        await db.SaveChangesAsync();

        var lounge = new MusicLoungeVenue
        {
            OwnerId = owner.Id, Name = $"Deadline-{Guid.NewGuid():N}"[..30], Status = LoungeStatus.Approved,
            Address = new VenueAddress { Street = "1 Test St", District = "1", City = "HCM" }
        };
        db.Lounges.Add(lounge);
        await db.SaveChangesAsync();

        db.Add(new BankAccount
        {
            OwnerType = BankAccountOwnerType.Lounge, OwnerId = lounge.Id, BankName = "Test Bank",
            AccountNumber = pii.Encrypt("0000000362"), AccountHolder = "Deadline Owner",
            IsDefault = true, IsVerified = true
        });

        var start = DateTimeOffset.UtcNow.AddHours(-1);
        var show = new LoungeShow
        {
            LoungeId = lounge.Id, Name = $"DeadlineShow-{Guid.NewGuid():N}", Description = "test",
            Format = LoungeShowFormat.Offline, Status = LoungeShowStatus.Ongoing,
            ScheduledStart = start, ScheduledEnd = start.AddHours(3), VcpmcRoyaltyReference = "VCPMC-TEST"
        };
        var performer = new Performer { Name = $"DeadlineArtist-{Guid.NewGuid():N}"[..25], CreatedByUserId = owner.Id };
        db.Add(show);
        db.Add(performer);
        await db.SaveChangesAsync();

        var performance = new Performance { LoungeShowId = show.Id, PerformerId = performer.Id };
        db.Add(performance);
        await db.SaveChangesAsync();

        return new Venue(owner.Id, lounge.Id, performance.Id);
    }

    private async Task<int> DonateAndConfirmAsync(int performanceId)
    {
        var audience = _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience");
        var res = await audience.PostAsJsonAsync("/api/v1/donations", new
        {
            PerformanceId = performanceId, Amount, IsAnonymous = false, Message = (string?)null, IsMessagePublic = true
        });
        res.StatusCode.Should().Be(HttpStatusCode.Created);
        var init = (await res.Content.ReadFromJsonAsync<Wrapped<InitData>>())!.Data;

        var ipn = await _factory.CreateClient().GetAsync(
            $"/api/v1/donations/vnpay-ipn?vnp_TxnRef={Uri.EscapeDataString(init.OrderId)}" +
            $"&vnp_ResponseCode=00&vnp_Amount={(long)(Amount * 100)}");
        ipn.StatusCode.Should().Be(HttpStatusCode.OK);
        return init.DonationId;
    }

    /// <summary>
    /// Đặt dòng tiền của một donate: nền tảng đã chuyển cho phòng trà lúc <paramref name="releasedAt"/>
    /// (null: chưa chuyển), cùng trạng thái và các mốc của donate.
    /// </summary>
    private async Task ArrangeAsync(
        int donationId, DateTimeOffset? releasedAt, DonationStatus status,
        DateTimeOffset? ownerAckAt, DateTimeOffset? paymentConfirmedAt = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var donation = await db.Donations.SingleAsync(d => d.Id == donationId);
        donation.Status = status;
        donation.OwnerAckAt = ownerAckAt;
        if (paymentConfirmedAt is { } confirmed) donation.PaymentConfirmedAt = confirmed;

        var payment = await db.Payments.SingleAsync(
            p => p.ReferenceType == "Donation" && p.ReferenceId == donationId.ToString());
        var settlement = await db.Settlements.SingleAsync(s => s.PaymentId == payment.Id);
        if (releasedAt is { } released)
        {
            settlement.Status = SettlementStatus.Released;
            settlement.ReleasedAt = released;
        }
        else
        {
            settlement.Status = SettlementStatus.Scheduled;
            settlement.ScheduledAt = DateTimeOffset.UtcNow.AddDays(1);
            settlement.ReleasedAt = null;
        }

        await db.SaveChangesAsync();
    }

    private async Task RunOverdueJobAsync()
    {
        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<DonationOverdueCheckJob>()
            .ExecuteAsync(new JobCancellationToken(false));
    }

    private async Task RunAutoConfirmJobAsync()
    {
        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AutoConfirmDonationsJob>()
            .ExecuteAsync(new JobCancellationToken(false));
    }

    private async Task<(bool Reminded, bool Warned)> OutcomeAsync(Venue venue, int donationId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var reminded = await db.Notifications.AnyAsync(n =>
            n.UserId == venue.OwnerId && n.Type == NotificationType.DonationPending
            && n.ReferenceId == donationId.ToString());
        var warned = await db.VenuePenalties.AnyAsync(p =>
            p.EvidenceRef == $"donation:{donationId}" && p.PenaltyType == PenaltyType.Warning);
        return (reminded, warned);
    }

    // ─── Job nhắc nhở / cảnh cáo ──────────────────────────────────────────────

    [Fact]
    public async Task Reminder_CountsFromWhenTheVenueWasPaid_NotFromWhenTheOwnerClicked()
    {
        var hold = await HoldDaysAsync();
        var venue = await SeedVenueAsync();
        var id = await DonateAndConfirmAsync(venue.PerformanceId);
        // Tiền về phòng trà từ hold+1 ngày trước; chủ mới bấm "đã nhận" hôm nay.
        await ArrangeAsync(id, releasedAt: DaysAgo(hold + 1), DonationStatus.OwnerReceived, ownerAckAt: DaysAgo(0));

        await RunOverdueJobAsync();

        var (reminded, warned) = await OutcomeAsync(venue, id);
        reminded.Should().BeTrue("hạn tính từ lúc tiền về phòng trà — bấm muộn không lùi được hạn");
        warned.Should().BeFalse("chưa tới mốc cảnh cáo (gấp đôi hạn)");
    }

    [Fact]
    public async Task Warning_AtTwiceTheHold_FromWhenTheVenueWasPaid()
    {
        var hold = await HoldDaysAsync();
        var venue = await SeedVenueAsync();
        var id = await DonateAndConfirmAsync(venue.PerformanceId);
        await ArrangeAsync(id, releasedAt: DaysAgo(2 * hold + 1), DonationStatus.OwnerReceived, ownerAckAt: DaysAgo(1));

        await RunOverdueJobAsync();

        (await OutcomeAsync(venue, id)).Warned.Should().BeTrue();
    }

    [Fact]
    public async Task StayingSilent_DoesNotBuyTheVenueExtraTime()
    {
        // Chủ không bao giờ bấm "đã nhận". Trước đây tự xác nhận đặt lại mốc tính hạn về hôm nay, nên
        // cảnh cáo bị lùi thêm cả một chu kỳ so với một chủ phòng trà xác nhận ngay.
        var hold = await HoldDaysAsync();
        var venue = await SeedVenueAsync();
        var id = await DonateAndConfirmAsync(venue.PerformanceId);
        await ArrangeAsync(id, releasedAt: DaysAgo(2 * hold + 1), DonationStatus.PendingOwnerAck, ownerAckAt: null);

        await RunAutoConfirmJobAsync();
        await RunOverdueJobAsync();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            (await db.Donations.SingleAsync(d => d.Id == id)).AutoConfirmed.Should().BeTrue();
        }
        (await OutcomeAsync(venue, id)).Warned.Should().BeTrue(
            "im lặng không được tính là thêm thời gian — mốc vẫn là lúc tiền về phòng trà");
    }

    // ─── Lịch sử của chủ ──────────────────────────────────────────────────────

    [Fact]
    public async Task OwnerHistory_ShowsTheSameDueDate_CountedFromThePayout()
    {
        var hold = await HoldDaysAsync();
        var venue = await SeedVenueAsync();
        var fresh = await DonateAndConfirmAsync(venue.PerformanceId);
        var late = await DonateAndConfirmAsync(venue.PerformanceId);
        // VNPay xác nhận từ 30 ngày trước, nhưng nền tảng mới chuyển cho phòng trà 3 ngày trước.
        var freshReleased = DaysAgo(3);
        await ArrangeAsync(fresh, freshReleased, DonationStatus.OwnerReceived, DaysAgo(2), paymentConfirmedAt: DaysAgo(30));
        await ArrangeAsync(late, DaysAgo(hold + 3), DonationStatus.OwnerReceived, DaysAgo(hold + 2), paymentConfirmedAt: DaysAgo(30));

        var owner = _factory.CreateAuthenticatedClient(venue.OwnerId, "Owner", venue.LoungeId);
        var res = await owner.GetAsync("/api/v1/donations/owner-history?pageSize=50");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = (await res.Content.ReadFromJsonAsync<Wrapped<History>>())!.Data.Items.Items;

        var freshItem = items.Single(i => i.Id == fresh);
        freshItem.PayoutStatus.Should().Be("WithinHoldPeriod",
            "phòng trà mới nhận tiền 3 ngày — chưa thể trễ hạn chỉ vì khán giả trả tiền từ 30 ngày trước");
        freshItem.PayoutDueAt!.Value.Should().BeCloseTo(freshReleased.AddDays(hold), TimeSpan.FromMinutes(1));

        items.Single(i => i.Id == late).PayoutStatus.Should().Be("Overdue");
    }

    // ─── Khiếu nại "chưa được trả" ────────────────────────────────────────────

    [Fact]
    public async Task NotPaidComplaint_OpensExactlyWhenTheDeadlinePasses()
    {
        var hold = await HoldDaysAsync();
        var venue = await SeedVenueAsync();
        var unreleased = await DonateAndConfirmAsync(venue.PerformanceId);
        var fresh = await DonateAndConfirmAsync(venue.PerformanceId);
        var late = await DonateAndConfirmAsync(venue.PerformanceId);
        await ArrangeAsync(unreleased, releasedAt: null, DonationStatus.PendingOwnerAck, null, paymentConfirmedAt: DaysAgo(30));
        await ArrangeAsync(fresh, DaysAgo(3), DonationStatus.OwnerReceived, DaysAgo(2), paymentConfirmedAt: DaysAgo(30));
        await ArrangeAsync(late, DaysAgo(hold + 1), DonationStatus.OwnerReceived, DaysAgo(hold));

        var audience = _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience");
        Task<HttpResponseMessage> ComplainAsync(int donationId) => audience.PostAsJsonAsync("/api/v1/complaints", new
        {
            TargetType = "donation", TargetId = donationId, Category = "DonationNotPaid",
            Description = "Nghệ sĩ báo chưa nhận được tiền donate", EvidenceUrls = (string?)null,
            ContactPhone = (string?)null
        });

        (await ComplainAsync(unreleased)).StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "nền tảng chưa chuyển tiền cho phòng trà — phòng trà chưa có hạn nào để trễ");
        (await ComplainAsync(fresh)).StatusCode.Should().Be(HttpStatusCode.BadRequest, "còn trong hạn");
        (await ComplainAsync(late)).StatusCode.Should().Be(HttpStatusCode.Created, "đã quá hạn");
    }

    // ─── Hai danh sách chờ của chủ ────────────────────────────────────────────

    [Fact]
    public async Task PendingList_ShowsTheRealAutoConfirmDeadline()
    {
        var hold = await HoldDaysAsync();
        var venue = await SeedVenueAsync();
        var id = await DonateAndConfirmAsync(venue.PerformanceId);
        var owner = _factory.CreateAuthenticatedClient(venue.OwnerId, "Owner", venue.LoungeId);

        async Task<PendingItem> ItemAsync()
        {
            var res = await owner.GetAsync("/api/v1/donations/pending-ack?pageSize=50");
            res.StatusCode.Should().Be(HttpStatusCode.OK);
            return (await res.Content.ReadFromJsonAsync<Wrapped<Page<PendingItem>>>())!.Data.Items.Single(i => i.Id == id);
        }

        await ArrangeAsync(id, releasedAt: null, DonationStatus.PendingOwnerAck, null);
        var beforePayout = await ItemAsync();
        beforePayout.PayoutReceivedAt.Should().BeNull();
        beforePayout.AutoConfirmDeadline.Should().BeNull(
            "nền tảng chưa chuyển thì chưa có hạn nào — không phải \"còn 24 giờ\" như trước");

        var released = DaysAgo(1);
        await ArrangeAsync(id, released, DonationStatus.PendingOwnerAck, null);
        var afterPayout = await ItemAsync();
        afterPayout.PayoutReceivedAt!.Value.Should().BeCloseTo(released, TimeSpan.FromMinutes(1));
        afterPayout.AutoConfirmDeadline!.Value.Should().BeCloseTo(released.AddDays(hold), TimeSpan.FromMinutes(1));
        afterPayout.PayoutDueAt.Should().Be(afterPayout.AutoConfirmDeadline, "một hạn duy nhất");
    }

    [Fact]
    public async Task AwaitingPayoutList_PutsThePaymentTimeInThePaymentField()
    {
        var hold = await HoldDaysAsync();
        var venue = await SeedVenueAsync();
        var id = await DonateAndConfirmAsync(venue.PerformanceId);
        var paid = DaysAgo(5);
        var released = DaysAgo(2);
        await ArrangeAsync(id, released, DonationStatus.OwnerReceived, ownerAckAt: DateTimeOffset.UtcNow.AddHours(-1),
            paymentConfirmedAt: paid);

        var owner = _factory.CreateAuthenticatedClient(venue.OwnerId, "Owner", venue.LoungeId);
        var res = await owner.GetAsync("/api/v1/donations/awaiting-payout?pageSize=50");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var item = (await res.Content.ReadFromJsonAsync<Wrapped<Page<PendingItem>>>())!.Data.Items.Single(i => i.Id == id);

        item.PaymentConfirmedAt!.Value.Should().BeCloseTo(paid, TimeSpan.FromMinutes(1),
            "trường tên PaymentConfirmedAt phải chứa lúc thanh toán — trước đây nó chứa lúc chủ bấm \"đã nhận\"");
        item.PayoutReceivedAt!.Value.Should().BeCloseTo(released, TimeSpan.FromMinutes(1));
        item.PayoutDueAt!.Value.Should().BeCloseTo(released.AddDays(hold), TimeSpan.FromMinutes(1));
    }
}
