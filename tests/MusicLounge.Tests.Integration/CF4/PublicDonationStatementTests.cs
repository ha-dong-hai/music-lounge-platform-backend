using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
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
using Serilog.Events;
using MusicLoungeVenue = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Tests.Integration.CF4;

/// <summary>
/// MLACP-365. Trang công khai trước đây chỉ có số tiền, trạng thái và ngày tạo: không biết tiền đang ở
/// đâu, đã nằm ở phòng trà bao lâu, nghệ sĩ được bao nhiêu; "PerformerPaid" hiện như sự thật dù chỉ là
/// lời khai của phòng trà; donate nền tảng đang giữ không hiện.
///
/// <para>Các bài đi qua đúng đường thật (tạo donate → IPN → job giải ngân → xác nhận → báo đã chuyển →
/// nghệ sĩ trả lời) và đọc trang công khai như một người chưa đăng nhập.</para>
/// </summary>
[Collection("Integration")]
public sealed class PublicDonationStatementTests
{
    private const decimal Amount = 100_000m;
    private const string PerformerAccountNumber = "0000003651";
    private const string VenueAccountNumber = "0000003652";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly ApiFactory _factory;

    public PublicDonationStatementTests(ApiFactory factory) => _factory = factory;

    private sealed record Venue(int OwnerId, int LoungeId, int PerformerId, int PerformanceId);

    private sealed record InitData(int DonationId, string OrderId);

    private sealed record Wrapped<T>(T Data);

    private sealed record Page(List<Entry> Items, int TotalCount);

    private sealed record Entry(
        int Id, string? DonorDisplayName, decimal? Gross, string Status, DateTimeOffset? PaidAt, string? Message,
        decimal? PlatformFee, decimal? TaxWithheld, decimal? PerformerAmount, decimal? VenueRetained,
        DateTimeOffset? PlatformPaidVenueAt, DateTimeOffset? VenueAcknowledgedAt, bool VenueAcknowledgedAutomatically,
        DateTimeOffset? PayoutDueAt, DateTimeOffset? VenueReportedPaidAt, bool HasTransferReceipt, bool Overdue,
        bool PaidLate, bool PerformerAskedToConfirm, string? PerformerResponse, DateTimeOffset? PerformerRespondedAt,
        string Stage, string StageLabel);

    private sealed record Policy(
        decimal PerformerShareRate, decimal PlatformCommissionRate, int VenuePayoutDays, int VenueWarningDays,
        bool Refundable, bool AmountAlwaysPublic, List<string> Statements);

    private sealed record Summary(
        int DonationCount, int DonationsWithHiddenAmount, decimal TotalGross, decimal TotalForPerformer,
        decimal HeldByPlatform, decimal HeldByVenue, decimal OverdueAtVenue, int OverdueCount,
        decimal ReportedPaidToPerformer, decimal ConfirmedByPerformer, decimal DisputedByPerformer,
        int PaidLateCount, Policy Policy);

    // ─── Dựng dữ liệu ─────────────────────────────────────────────────────────

    private async Task<Venue> SeedVenueAsync(string? performerEmail = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var pii = scope.ServiceProvider.GetRequiredService<IPiiEncryptionService>();

        var owner = new User { Email = $"statement-owner-{Guid.NewGuid():N}@test.com", FullName = "Statement Owner" };
        db.Users.Add(owner);
        await db.SaveChangesAsync();

        var lounge = new MusicLoungeVenue
        {
            OwnerId = owner.Id, Name = $"Statement-{Guid.NewGuid():N}"[..30], Status = LoungeStatus.Approved,
            Address = new VenueAddress { Street = "1 Test St", District = "1", City = "HCM" }
        };
        db.Lounges.Add(lounge);
        await db.SaveChangesAsync();

        var start = DateTimeOffset.UtcNow.AddHours(-1);
        var show = new LoungeShow
        {
            LoungeId = lounge.Id, Name = $"StatementShow-{Guid.NewGuid():N}", Description = "test",
            Format = LoungeShowFormat.Offline, Status = LoungeShowStatus.Ongoing,
            ScheduledStart = start, ScheduledEnd = start.AddHours(3), VcpmcRoyaltyReference = "VCPMC-TEST"
        };
        var performer = new Performer
        {
            Name = $"StatementArtist-{Guid.NewGuid():N}"[..25], CreatedByUserId = owner.Id, ContactEmail = performerEmail
        };
        db.Add(show);
        db.Add(performer);
        await db.SaveChangesAsync();

        db.Add(new BankAccount
        {
            OwnerType = BankAccountOwnerType.Lounge, OwnerId = lounge.Id, BankName = "Test Bank",
            AccountNumber = pii.Encrypt(VenueAccountNumber), AccountHolder = "Statement Owner", IsDefault = true
        });
        db.Add(new BankAccount
        {
            OwnerType = BankAccountOwnerType.Performer, OwnerId = performer.Id, BankName = "Test Bank",
            AccountNumber = pii.Encrypt(PerformerAccountNumber), AccountHolder = "Statement Artist", IsDefault = true
        });
        var performance = new Performance { LoungeShowId = show.Id, PerformerId = performer.Id };
        db.Add(performance);
        await db.SaveChangesAsync();

        return new Venue(owner.Id, lounge.Id, performer.Id, performance.Id);
    }

    private HttpClient OwnerOf(Venue venue) => _factory.CreateAuthenticatedClient(venue.OwnerId, "Owner", venue.LoungeId);

    private async Task<InitData> CreateAsync(
        int performanceId, string? message = "Cảm ơn!", bool anonymous = false, bool messagePublic = true)
    {
        var res = await _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience").PostAsJsonAsync(
            "/api/v1/donations",
            new { PerformanceId = performanceId, Amount, IsAnonymous = anonymous, Message = message, IsMessagePublic = messagePublic });
        res.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await res.Content.ReadFromJsonAsync<Wrapped<InitData>>())!.Data;
    }

    private async Task IpnAsync(string orderId, string responseCode = "00", string? transactionNo = null)
    {
        var url = $"/api/v1/donations/vnpay-ipn?vnp_TxnRef={Uri.EscapeDataString(orderId)}" +
                  $"&vnp_ResponseCode={responseCode}&vnp_Amount={(long)(Amount * 100)}" +
                  (transactionNo is null ? "" : $"&vnp_TransactionNo={transactionNo}");
        (await _factory.CreateClient().GetAsync(url)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private async Task<int> DonateAsync(
        int performanceId, string? message = "Cảm ơn!", bool anonymous = false, bool messagePublic = true,
        string? transactionNo = null)
    {
        var init = await CreateAsync(performanceId, message, anonymous, messagePublic);
        await IpnAsync(init.OrderId, transactionNo: transactionNo);
        return init.DonationId;
    }

    private async Task RunReleaseJobAsync()
    {
        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<SettlementReleaseJob>()
            .ExecuteAsync(new JobCancellationToken(false));
    }

    private async Task AcknowledgeAsync(Venue venue, int donationId)
        => (await OwnerOf(venue).PostAsync($"/api/v1/donations/{donationId}/acknowledge", null))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

    private async Task ReportPaidAsync(Venue venue, int donationId, string paymentRef = "CK-365", string? evidenceUrl = null)
        => (await OwnerOf(venue).PostAsJsonAsync($"/api/v1/donations/{donationId}/confirm-paid",
                new { PaymentRef = paymentRef, PaymentEvidenceUrl = evidenceUrl }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

    private async Task BackdateVenuePayoutAsync(int donationId, TimeSpan ago)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var payment = await db.Payments.SingleAsync(p => p.ReferenceType == "Donation" && p.ReferenceId == donationId.ToString());
        var settlement = await db.Settlements.SingleAsync(s => s.PaymentId == payment.Id);
        settlement.ReleasedAt = DateTimeOffset.UtcNow - ago;
        await db.SaveChangesAsync();
    }

    private async Task<int> HoldDaysAsync()
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ISystemConfigService>()
            .GetIntAsync("donation_hold_days", 7, CancellationToken.None);
    }

    /// <summary>Token của liên kết mới nhất đã gửi tới hộp thư này (SMTP không cấu hình → ghi log).</summary>
    private static string LatestTokenSentTo(string email)
    {
        var sent = CapturingLogSink.Snapshot()
            .Where(e => e.Properties.TryGetValue("ConfirmationLink", out _)
                        && e.Properties.TryGetValue("Email", out var to)
                        && to is ScalarValue { Value: string address } && address == email)
            .LastOrDefault();
        sent.Should().NotBeNull($"phải có một liên kết xác nhận được gửi tới {email}");
        var link = (string)((ScalarValue)sent!.Properties["ConfirmationLink"]).Value!;
        return Uri.UnescapeDataString(link[(link.IndexOf("token=", StringComparison.Ordinal) + "token=".Length)..]);
    }

    private Task<HttpResponseMessage> PerformerRespondsAsync(string email, string decision)
        => _factory.CreateClient().PostAsJsonAsync("/api/v1/performer-confirmations/respond",
            new { Token = LatestTokenSentTo(email), Decision = decision, ConsentToDataProcessing = true });

    // ─── Đọc trang công khai (không đăng nhập) ────────────────────────────────

    private async Task<(Page Page, string Raw)> ListAsync(int performerId)
    {
        var res = await _factory.CreateClient().GetAsync($"/api/v1/performers/{performerId}/donations?pageSize=100");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var raw = await res.Content.ReadAsStringAsync();
        return (JsonSerializer.Deserialize<Wrapped<Page>>(raw, Json)!.Data, raw);
    }

    private async Task<Entry> EntryAsync(int performerId, int donationId)
        => (await ListAsync(performerId)).Page.Items.Single(e => e.Id == donationId);

    private async Task<(Summary Summary, string Raw)> SummaryAsync(int performerId)
    {
        var res = await _factory.CreateClient().GetAsync($"/api/v1/performers/{performerId}/donations/summary");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var raw = await res.Content.ReadAsStringAsync();
        return (JsonSerializer.Deserialize<Wrapped<Summary>>(raw, Json)!.Data, raw);
    }

    // ─── Dòng thời gian ───────────────────────────────────────────────────────

    [Fact]
    public async Task Timeline_FollowsTheMoney_FromVnPay_ToTheVenue_ToWhatTheVenueReports()
    {
        var venue = await SeedVenueAsync();
        var holdDays = await HoldDaysAsync();
        var donationId = await DonateAsync(venue.PerformanceId, "Hay quá!");

        // VNPay đã xác nhận, nền tảng đang giữ — trước đây khoản này không hề hiện.
        var held = await EntryAsync(venue.PerformerId, donationId);
        held.Status.Should().Be("PendingOwnerAck");
        held.Stage.Should().Be("PlatformHolding");
        held.PaidAt.Should().NotBeNull();
        held.PlatformPaidVenueAt.Should().BeNull();
        held.PayoutDueAt.Should().BeNull("phòng trà chưa nhận tiền thì chưa có hạn chuyển cho nghệ sĩ");
        held.Message.Should().Be("Hay quá!");
        held.Gross.Should().Be(Amount);
        held.PerformerAmount.Should().Be(88_000m);
        (held.PlatformFee + held.TaxWithheld + held.PerformerAmount + held.VenueRetained).Should().Be(Amount,
            "phí nền tảng + thuế + phần nghệ sĩ + phần phòng trà giữ lại phải đúng bằng số tiền donate");

        // Nền tảng chuyển cho phòng trà: đồng hồ bắt đầu chạy từ đây, không phải từ cú bấm "đã nhận".
        await RunReleaseJobAsync();
        var atVenue = await EntryAsync(venue.PerformerId, donationId);
        atVenue.Stage.Should().Be("VenueHolding", "phòng trà đã có tiền dù chưa bấm \"đã nhận\"");
        atVenue.PlatformPaidVenueAt.Should().NotBeNull();
        atVenue.PayoutDueAt.Should().Be(atVenue.PlatformPaidVenueAt!.Value.AddDays(holdDays));
        atVenue.VenueAcknowledgedAt.Should().BeNull();

        await AcknowledgeAsync(venue, donationId);
        var acknowledged = await EntryAsync(venue.PerformerId, donationId);
        acknowledged.VenueAcknowledgedAt.Should().NotBeNull();
        acknowledged.VenueAcknowledgedAutomatically.Should().BeFalse();
        acknowledged.PayoutDueAt.Should().Be(atVenue.PayoutDueAt, "cú bấm \"đã nhận\" không dời hạn");

        await ReportPaidAsync(venue, donationId, evidenceUrl: "https://bank.example/receipt/365");
        var reported = await EntryAsync(venue.PerformerId, donationId);
        reported.Status.Should().Be("PerformerPaid");
        reported.Stage.Should().Be("VenueReportedPaid");
        reported.VenueReportedPaidAt.Should().NotBeNull();
        reported.HasTransferReceipt.Should().BeTrue();
        reported.Overdue.Should().BeFalse();
        reported.PaidLate.Should().BeFalse();
        reported.PerformerAskedToConfirm.Should().BeFalse("nghệ sĩ này không có email");
        reported.StageLabel.Should().Be("Phòng trà báo đã chuyển — nghệ sĩ chưa được mời xác nhận",
            "đó là lời khai của phòng trà — trang không được nói là nghệ sĩ đã nhận");

        // Số phòng trà khai đã chuyển là số đã ghi vào nhật ký lúc báo, không tính lại từ dữ liệu về sau.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var donation = await db.Donations.SingleAsync(d => d.Id == donationId);
            donation.PerformerShareRateSnapshot = 0.5m;
            await db.SaveChangesAsync();
        }
        (await EntryAsync(venue.PerformerId, donationId)).PerformerAmount.Should().Be(88_000m);
    }

    [Fact]
    public async Task WhatThePageSaysTheArtistGot_IsTheArtistsOwnAnswer()
    {
        var email = $"statement-artist-{Guid.NewGuid():N}@test.com";
        var venue = await SeedVenueAsync(email);

        var confirmedId = await DonateAsync(venue.PerformanceId);
        var disputedId = await DonateAsync(venue.PerformanceId);
        await RunReleaseJobAsync();

        await AcknowledgeAsync(venue, confirmedId);
        await ReportPaidAsync(venue, confirmedId);
        var awaiting = await EntryAsync(venue.PerformerId, confirmedId);
        awaiting.PerformerAskedToConfirm.Should().BeTrue();
        awaiting.PerformerResponse.Should().BeNull();
        awaiting.StageLabel.Should().Be("Phòng trà báo đã chuyển — nghệ sĩ chưa phản hồi");

        (await PerformerRespondsAsync(email, "Confirm")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        var confirmed = await EntryAsync(venue.PerformerId, confirmedId);
        confirmed.Stage.Should().Be("PerformerConfirmed");
        confirmed.PerformerResponse.Should().Be("Confirmed");
        confirmed.PerformerRespondedAt.Should().NotBeNull();

        await AcknowledgeAsync(venue, disputedId);
        await ReportPaidAsync(venue, disputedId);
        (await PerformerRespondsAsync(email, "Dispute")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        var disputed = await EntryAsync(venue.PerformerId, disputedId);
        disputed.Stage.Should().Be("PerformerDisputed");
        disputed.PerformerResponse.Should().Be("Disputed");
        disputed.StageLabel.Should().Be("Nghệ sĩ báo chưa nhận được — đã mở khiếu nại");
    }

    [Fact]
    public async Task MoneyLeftAtTheVenuePastTheDeadline_IsShownOverdue_AndPayingLateStaysOnRecord()
    {
        var venue = await SeedVenueAsync();
        var holdDays = await HoldDaysAsync();
        var donationId = await DonateAsync(venue.PerformanceId);
        await RunReleaseJobAsync();
        await BackdateVenuePayoutAsync(donationId, TimeSpan.FromDays(holdDays + 3));

        var overdue = await EntryAsync(venue.PerformerId, donationId);
        overdue.Overdue.Should().BeTrue();
        overdue.Stage.Should().Be("VenueHolding");
        overdue.StageLabel.Should().Be("Phòng trà đang giữ — đã quá hạn chuyển cho nghệ sĩ");
        var (summary, _) = await SummaryAsync(venue.PerformerId);
        summary.OverdueCount.Should().Be(1);
        summary.OverdueAtVenue.Should().Be(88_000m);
        summary.HeldByVenue.Should().Be(88_000m);

        await AcknowledgeAsync(venue, donationId);
        await ReportPaidAsync(venue, donationId);
        var late = await EntryAsync(venue.PerformerId, donationId);
        late.Overdue.Should().BeFalse();
        late.PaidLate.Should().BeTrue("chuyển rồi cũng không xoá được việc đã chuyển trễ");
        (await SummaryAsync(venue.PerformerId)).Summary.PaidLateCount.Should().Be(1);
    }

    // ─── Nội dung công khai ───────────────────────────────────────────────────

    [Fact]
    public async Task PublicPage_ShowsAmounts_ButNeverAccountNumbers_TransferRefs_TransactionIds_OrReceipts()
    {
        var venue = await SeedVenueAsync();
        var transactionNo = $"T{Guid.NewGuid():N}"[..14];
        var donationId = await DonateAsync(venue.PerformanceId, anonymous: true, transactionNo: transactionNo);
        await RunReleaseJobAsync();
        await AcknowledgeAsync(venue, donationId);
        await ReportPaidAsync(venue, donationId, "CK-SECRET-365", "https://bank.example/receipt/secret-365");

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            (await db.Payments.SingleAsync(p => p.ReferenceType == "Donation" && p.ReferenceId == donationId.ToString()))
                .TransactionId.Should().Be(transactionNo, "mã giao dịch phải thật sự được lưu thì kiểm tra dưới đây mới có nghĩa");
        }

        var (page, listRaw) = await ListAsync(venue.PerformerId);
        var (_, summaryRaw) = await SummaryAsync(venue.PerformerId);
        foreach (var secret in new[] { transactionNo, "CK-SECRET-365", "secret-365", PerformerAccountNumber, VenueAccountNumber })
        {
            listRaw.Should().NotContain(secret);
            summaryRaw.Should().NotContain(secret);
        }

        var entry = page.Items.Single(e => e.Id == donationId);
        entry.DonorDisplayName.Should().BeNull("người donate chọn ẩn danh");
        entry.Gross.Should().Be(Amount, "ẩn danh chỉ ẩn tên, số tiền vẫn công khai");
        entry.HasTransferReceipt.Should().BeTrue("có chứng từ lưu trữ — bản thân chứng từ không công khai");
    }

    [Fact]
    public async Task Messages_FollowTheDonorsChoice_AndTheSameModerationAsTheLivestream()
    {
        var venue = await SeedVenueAsync();
        var admin = _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin");
        (await admin.PutAsJsonAsync("/api/v1/admin/system-config/donation_message_blocked_words",
                new { ConfigValue = "[\"tu khoa cam\"]", Note = "Kiểm tra lời nhắn trên trang sao kê công khai" }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        try
        {
            var publicId = await DonateAsync(venue.PerformanceId, "Hay quá!");
            var blockedId = await DonateAsync(venue.PerformanceId, "Đây là TỪ KHÓA CẤM nhé");
            var hiddenId = await DonateAsync(venue.PerformanceId, "Sẽ bị gỡ");
            var privateId = await DonateAsync(venue.PerformanceId, "Riêng tư", messagePublic: false);
            (await OwnerOf(venue).PostAsync($"/api/v1/donations/{hiddenId}/hide-message", null))
                .StatusCode.Should().Be(HttpStatusCode.NoContent);

            var items = (await ListAsync(venue.PerformerId)).Page.Items;
            items.Single(e => e.Id == publicId).Message.Should().Be("Hay quá!");
            items.Single(e => e.Id == blockedId).Message.Should().BeNull("chứa từ cấm");
            items.Single(e => e.Id == hiddenId).Message.Should().BeNull("đã gỡ khỏi livestream thì không hiện lại ở đây");
            items.Single(e => e.Id == privateId).Message.Should().BeNull("người donate không cho công khai");
        }
        finally
        {
            (await admin.PutAsJsonAsync("/api/v1/admin/system-config/donation_message_blocked_words",
                    new { ConfigValue = "[]", Note = "Kiểm tra lời nhắn trên trang sao kê công khai" }))
                .StatusCode.Should().Be(HttpStatusCode.NoContent);
        }
    }

    [Fact]
    public async Task OldDonationWithAHiddenAmount_StaysHidden_AndIsLeftOutOfTheTotals()
    {
        var venue = await SeedVenueAsync();
        var visibleId = await DonateAsync(venue.PerformanceId);
        var hiddenId = await DonateAsync(venue.PerformanceId);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            (await db.Donations.SingleAsync(d => d.Id == hiddenId)).IsAmountPublic = false;
            await db.SaveChangesAsync();
        }

        var hidden = await EntryAsync(venue.PerformerId, hiddenId);
        hidden.Gross.Should().BeNull();
        hidden.PerformerAmount.Should().BeNull();
        hidden.PlatformFee.Should().BeNull();
        hidden.TaxWithheld.Should().BeNull();
        hidden.VenueRetained.Should().BeNull();
        (await EntryAsync(venue.PerformerId, visibleId)).Gross.Should().Be(Amount);

        var (summary, _) = await SummaryAsync(venue.PerformerId);
        summary.DonationCount.Should().Be(2);
        summary.DonationsWithHiddenAmount.Should().Be(1);
        summary.TotalGross.Should().Be(Amount);
    }

    // ─── Tổng hợp và chính sách ───────────────────────────────────────────────

    [Fact]
    public async Task Summary_AddsUpWhereTheArtistsMoneyIs_AndPublishesThePolicyInForce()
    {
        var email = $"summary-artist-{Guid.NewGuid():N}@test.com";
        var venue = await SeedVenueAsync(email);

        var paidId = await DonateAsync(venue.PerformanceId);
        await DonateAsync(venue.PerformanceId);                 // để ở phòng trà
        await RunReleaseJobAsync();
        await DonateAsync(venue.PerformanceId);                 // nền tảng còn giữ
        await CreateAsync(venue.PerformanceId);                 // chưa thanh toán — không tính
        var failed = await CreateAsync(venue.PerformanceId);
        await IpnAsync(failed.OrderId, responseCode: "24");     // thất bại — không tính

        await AcknowledgeAsync(venue, paidId);
        await ReportPaidAsync(venue, paidId);
        (await PerformerRespondsAsync(email, "Confirm")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await ListAsync(venue.PerformerId)).Page.TotalCount.Should().Be(3);

        var (summary, _) = await SummaryAsync(venue.PerformerId);
        summary.DonationCount.Should().Be(3);
        summary.TotalGross.Should().Be(3 * Amount);
        summary.TotalForPerformer.Should().Be(3 * 88_000m);
        summary.HeldByPlatform.Should().Be(88_000m);
        summary.HeldByVenue.Should().Be(88_000m);
        summary.ReportedPaidToPerformer.Should().Be(88_000m);
        summary.ConfirmedByPerformer.Should().Be(88_000m);
        summary.DisputedByPerformer.Should().Be(0m);
        summary.OverdueCount.Should().Be(0);

        using var scope = _factory.Services.CreateScope();
        var config = scope.ServiceProvider.GetRequiredService<ISystemConfigService>();
        var holdDays = await config.GetIntAsync("donation_hold_days", 7, CancellationToken.None);
        summary.Policy.PerformerShareRate.Should().Be(
            await config.GetDecimalAsync("donation_performer_share_rate", 0.88m, CancellationToken.None));
        summary.Policy.VenuePayoutDays.Should().Be(holdDays);
        summary.Policy.VenueWarningDays.Should().Be(2 * holdDays, "đúng mốc job cảnh cáo phòng trà");
        summary.Policy.Refundable.Should().BeFalse("không có đường hoàn tiền donate nào trong hệ thống");
        summary.Policy.AmountAlwaysPublic.Should().BeTrue();
        summary.Policy.Statements.Should().Contain(s => s.Contains("88%"));
    }

    [Fact]
    public async Task Summary_ForAnUnknownPerformer_Is404()
        => (await _factory.CreateClient().GetAsync($"/api/v1/performers/{int.MaxValue}/donations/summary"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
}
