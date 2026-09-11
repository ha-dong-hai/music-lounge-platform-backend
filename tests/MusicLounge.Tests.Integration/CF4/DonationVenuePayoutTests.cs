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
/// MLACP-361. Tiền donate vào merchant VNPay của nền tảng, nhưng không có gì chuyển phần của phòng
/// trà đi: sổ cái ghi Có thẳng cho chủ phòng trà ngay lúc VNPay xác nhận, không có khoản quyết toán
/// nào, và chủ phòng trà bị yêu cầu "xác nhận đã nhận" một khoản chưa từng được chuyển.
///
/// <para>Mỗi bài dựng một phòng trà riêng (có hoặc không có tài khoản ngân hàng) với một buổi diễn
/// đang diễn ra, đi qua đúng đường thật: tạo donate → IPN VNPay → job giải ngân → xác nhận.</para>
/// </summary>
[Collection("Integration")]
public sealed class DonationVenuePayoutTests
{
    private const decimal Amount = 100_000m;

    private readonly ApiFactory _factory;

    public DonationVenuePayoutTests(ApiFactory factory) => _factory = factory;

    private sealed record Venue(int OwnerId, int LoungeId, int PerformanceId, int? BankAccountId);

    private sealed record InitData(int DonationId, string OrderId);

    private sealed record Wrapped<T>(T Data);

    private sealed record ReportSlice(decimal TotalSettlementReceived, decimal TotalPlatformFeePaid);

    private async Task<Venue> SeedVenueAsync(bool withBankAccount)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var owner = new User { Email = $"payout-owner-{Guid.NewGuid():N}@test.com", FullName = "Payout Owner" };
        db.Users.Add(owner);
        await db.SaveChangesAsync();

        var lounge = new MusicLoungeVenue
        {
            OwnerId = owner.Id, Name = $"Payout-{Guid.NewGuid():N}"[..30], Status = LoungeStatus.Approved,
            Address = new VenueAddress { Street = "1 Test St", District = "1", City = "HCM" }
        };
        db.Lounges.Add(lounge);
        await db.SaveChangesAsync();

        int? bankAccountId = null;
        if (withBankAccount)
        {
            var pii = scope.ServiceProvider.GetRequiredService<IPiiEncryptionService>();
            var account = new BankAccount
            {
                OwnerType = BankAccountOwnerType.Lounge, OwnerId = lounge.Id, BankName = "Test Bank",
                AccountNumber = pii.Encrypt("0000000361"), AccountHolder = "Payout Owner",
                IsDefault = true, IsVerified = true
            };
            db.Add(account);
            await db.SaveChangesAsync();
            bankAccountId = account.Id;
        }

        var start = DateTimeOffset.UtcNow.AddHours(-1);
        var show = new LoungeShow
        {
            LoungeId = lounge.Id, Name = $"PayoutShow-{Guid.NewGuid():N}", Description = "test",
            Format = LoungeShowFormat.Offline, Status = LoungeShowStatus.Ongoing,
            ScheduledStart = start, ScheduledEnd = start.AddHours(3), VcpmcRoyaltyReference = "VCPMC-TEST"
        };
        var performer = new Performer { Name = $"PayoutArtist-{Guid.NewGuid():N}"[..25], CreatedByUserId = owner.Id };
        db.Add(show);
        db.Add(performer);
        await db.SaveChangesAsync();

        var performance = new Performance { LoungeShowId = show.Id, PerformerId = performer.Id };
        db.Add(performance);
        await db.SaveChangesAsync();

        return new Venue(owner.Id, lounge.Id, performance.Id, bankAccountId);
    }

    private async Task<int> DonateAndConfirmAsync(int performanceId)
    {
        var audience = _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience");
        var res = await audience.PostAsJsonAsync("/api/v1/donations", new
        {
            PerformanceId = performanceId, Amount, IsAnonymous = false, Message = "Cảm ơn!", IsMessagePublic = true
        });
        res.StatusCode.Should().Be(HttpStatusCode.Created);
        var init = (await res.Content.ReadFromJsonAsync<Wrapped<InitData>>())!.Data;

        var ipn = await _factory.CreateClient().GetAsync(
            $"/api/v1/donations/vnpay-ipn?vnp_TxnRef={Uri.EscapeDataString(init.OrderId)}" +
            $"&vnp_ResponseCode=00&vnp_Amount={(long)(Amount * 100)}");
        ipn.StatusCode.Should().Be(HttpStatusCode.OK);
        return init.DonationId;
    }

    private async Task RunReleaseJobAsync()
    {
        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<SettlementReleaseJob>()
            .ExecuteAsync(new JobCancellationToken(false));
    }

    private async Task RunAutoConfirmJobAsync()
    {
        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AutoConfirmDonationsJob>()
            .ExecuteAsync(new JobCancellationToken(false));
    }

    private static async Task<(Payment Payment, Settlement Settlement)> PayoutOfAsync(ApplicationDbContext db, int donationId)
    {
        var payment = await db.Payments.SingleAsync(
            p => p.ReferenceType == "Donation" && p.ReferenceId == donationId.ToString());
        var settlement = await db.Settlements.SingleAsync(s => s.PaymentId == payment.Id);
        return (payment, settlement);
    }

    private Task<HttpResponseMessage> AcknowledgeAsync(Venue venue, int donationId)
        => _factory.CreateAuthenticatedClient(venue.OwnerId, "Owner", venue.LoungeId)
            .PostAsync($"/api/v1/donations/{donationId}/acknowledge", null);

    [Fact]
    public async Task VnPayConfirmation_HoldsTheVenuesShareOnThePlatform_AndSchedulesARealPayout()
    {
        var venue = await SeedVenueAsync(withBankAccount: true);

        var donationId = await DonateAndConfirmAsync(venue.PerformanceId);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var donation = await db.Donations.SingleAsync(d => d.Id == donationId);
        var (payment, settlement) = await PayoutOfAsync(db, donationId);

        payment.Status.Should().Be(PaymentStatus.Confirmed);
        payment.Method.Should().Be(PaymentMethod.Gateway);
        payment.GrossAmount.Should().Be(Amount);
        payment.NetAmount.Should().Be(donation.Net);
        (payment.PlatformFee + payment.TaxWithheld + payment.PersonalIncomeTaxWithheld + payment.NetAmount)
            .Should().Be(Amount, "mọi đồng khán giả trả phải có chỗ: phí, thuế, hoặc phần của phòng trà");
        payment.TransactionId.Should().NotBeNullOrWhiteSpace("mã giao dịch VNPay là bằng chứng đối soát với cổng");

        settlement.OwnerId.Should().Be(venue.OwnerId);
        settlement.ReleaseType.Should().Be(SettlementReleaseType.Full);
        settlement.NetAmount.Should().Be(payment.NetAmount);
        settlement.BankAccountId.Should().Be(venue.BankAccountId, "chuyển vào đúng tài khoản mặc định của phòng trà");
        settlement.Status.Should().Be(SettlementStatus.Scheduled);

        var ownerCredits = await db.LedgerEntries
            .Where(e => e.PaymentId == payment.Id && e.Account.OwnerType == AccountType.User && !e.IsDebit)
            .CountAsync();
        ownerCredits.Should().Be(0, "chưa chuyển thì chưa có đồng nào thuộc về tài khoản chủ phòng trà");

        donation.Status.Should().Be(DonationStatus.PendingOwnerAck);
    }

    [Fact]
    public async Task OwnerConfirmsReceipt_OnlyAfterThePlatformHasPaidTheVenue()
    {
        var venue = await SeedVenueAsync(withBankAccount: true);
        var donationId = await DonateAndConfirmAsync(venue.PerformanceId);

        (await AcknowledgeAsync(venue, donationId)).StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "nền tảng chưa chuyển đồng nào — chủ phòng trà không thể đã nhận");

        await RunReleaseJobAsync();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var (_, settlement) = await PayoutOfAsync(db, donationId);
            settlement.Status.Should().Be(SettlementStatus.Released);

            var ownerAccount = await db.LedgerAccounts.SingleAsync(
                a => a.OwnerType == AccountType.User && a.OwnerId == venue.OwnerId);
            var payout = await db.LedgerEntries.SingleAsync(e =>
                e.ReferenceType == "settlement" && e.ReferenceId == settlement.Id.ToString()
                && e.AccountId == ownerAccount.Id);
            payout.IsDebit.Should().BeFalse();
            payout.Amount.Should().Be(settlement.NetAmount);

            var notice = await db.Notifications.SingleAsync(n =>
                n.UserId == venue.OwnerId && n.Type == NotificationType.SettlementReleased
                && n.ReferenceId == settlement.Id.ToString());
            notice.Body.Should().Contain($"donate #{donationId}",
                "chủ phòng trà phải biết đây là tiền donate — một phần phải chuyển tiếp cho nghệ sĩ");
        }

        (await AcknowledgeAsync(venue, donationId)).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task VenueWithoutABankAccount_ThePayoutWaits_AndNothingIsLost()
    {
        var venue = await SeedVenueAsync(withBankAccount: false);
        var donationId = await DonateAndConfirmAsync(venue.PerformanceId);

        await RunReleaseJobAsync();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var (_, settlement) = await PayoutOfAsync(db, donationId);
            settlement.BankAccountId.Should().BeNull();
            settlement.Status.Should().Be(SettlementStatus.Scheduled,
                "không có chỗ để chuyển thì hoãn — khoản nợ vẫn được ghi, lần chạy sau sẽ trả");
        }

        (await AcknowledgeAsync(venue, donationId)).StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task AutoConfirm_CountsFromThePayout_NotFromTheVnPayConfirmation()
    {
        var venue = await SeedVenueAsync(withBankAccount: true);
        var donationId = await DonateAndConfirmAsync(venue.PerformanceId);

        // VNPay xác nhận từ lâu, nhưng nền tảng chưa chuyển tiền cho phòng trà.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var donation = await db.Donations.SingleAsync(d => d.Id == donationId);
            donation.PaymentConfirmedAt = DateTimeOffset.UtcNow.AddDays(-30);
            var (_, settlement) = await PayoutOfAsync(db, donationId);
            settlement.ScheduledAt = DateTimeOffset.UtcNow.AddDays(1);
            await db.SaveChangesAsync();
        }

        await RunAutoConfirmJobAsync();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            (await db.Donations.SingleAsync(d => d.Id == donationId)).Status.Should().Be(
                DonationStatus.PendingOwnerAck,
                "không được đánh dấu \"phòng trà đã nhận\" một khoản còn nằm ở nền tảng");

            // Nền tảng đã chuyển từ lâu, phòng trà không phản hồi.
            var (_, settlement) = await PayoutOfAsync(db, donationId);
            settlement.Status = SettlementStatus.Released;
            settlement.ReleasedAt = DateTimeOffset.UtcNow.AddDays(-30);
            await db.SaveChangesAsync();
        }

        await RunAutoConfirmJobAsync();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var donation = await db.Donations.SingleAsync(d => d.Id == donationId);
            donation.Status.Should().Be(DonationStatus.OwnerReceived);
            donation.AutoConfirmed.Should().BeTrue();
        }
    }

    [Fact]
    public async Task RevenueReport_CountsTheDonationPayout_AsReceived_AndItsFeesAsPaid()
    {
        var venue = await SeedVenueAsync(withBankAccount: true);
        var donationId = await DonateAndConfirmAsync(venue.PerformanceId);
        var owner = _factory.CreateAuthenticatedClient(venue.OwnerId, "Owner", venue.LoungeId);

        async Task<ReportSlice> ReportAsync()
        {
            var res = await owner.GetAsync($"/api/v1/analytics/revenue-report?loungeId={venue.LoungeId}");
            res.StatusCode.Should().Be(HttpStatusCode.OK);
            return (await res.Content.ReadFromJsonAsync<Wrapped<ReportSlice>>())!.Data;
        }

        (await ReportAsync()).TotalSettlementReceived.Should().Be(0m, "chưa giải ngân thì chưa nhận");

        await RunReleaseJobAsync();

        decimal net;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            net = (await db.Donations.SingleAsync(d => d.Id == donationId)).Net;
        }

        var report = await ReportAsync();
        report.TotalSettlementReceived.Should().Be(net);
        report.TotalPlatformFeePaid.Should().Be(Amount - net, "hoa hồng và thuế khấu trừ trên donate");
    }
}
