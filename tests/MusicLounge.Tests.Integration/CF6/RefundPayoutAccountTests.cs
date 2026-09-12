using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
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
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Tests.Integration.CF6;

/// <summary>
/// MLACP-387. Luật BVQLNTD 2023 Điều 38 khoản 4: hoàn trả theo phương thức người tiêu dùng đã thanh toán, trừ khi họ
/// đồng ý phương thức khác. Khi VNPay đã quá hạn nhận lệnh hoàn, đường còn lại là chuyển khoản — nên người mua phải tự
/// khai tài khoản và đồng ý. Trước task này (MLACP-384) Admin chuyển tới tài khoản tự tìm, không có bằng chứng đồng ý.
/// </summary>
[Collection("Integration")]
public sealed class RefundPayoutAccountTests
{
    private const string AccountNumber = "0123456789";
    private const string PayoutRequestTitle = "Cần tài khoản ngân hàng để hoàn tiền cho bạn";
    private const string WindowExpiredTitle = "Hết hạn hoàn tiền qua VNPay";

    private readonly ApiFactory _factory;

    public RefundPayoutAccountTests(ApiFactory factory) => _factory = factory;

    private sealed record Seeded(int RefundId, int PaymentId, int BuyerId);

    private ApplicationDbContext Db(IServiceScope scope) => scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

    private async Task<int> NewUserAsync(string prefix)
    {
        using var scope = _factory.Services.CreateScope();
        var db = Db(scope);
        var user = new User { Email = $"{prefix}-{Guid.NewGuid():N}@test.com", FullName = "Người mua" };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }

    private async Task<Seeded> SeedAsync(int paidDaysAgo, PaymentMethod method = PaymentMethod.Gateway)
    {
        var buyerId = await NewUserAsync("b387");
        using var scope = _factory.Services.CreateScope();
        var db = Db(scope);

        var owner = new User { Email = $"o387-{Guid.NewGuid():N}@test.com", FullName = "Chủ phòng trà" };
        db.Users.Add(owner);
        await db.SaveChangesAsync();
        var lounge = new MusicLoungeEntity
        {
            OwnerId = owner.Id, Name = $"Venue387-{Guid.NewGuid():N}"[..30], Status = LoungeStatus.Approved,
            Address = new VenueAddress { Street = "1 Test St", District = "1", City = "HCM" }
        };
        db.Add(lounge);
        await db.SaveChangesAsync();
        var show = new LoungeShow
        {
            LoungeId = lounge.Id, Name = $"Đêm nhạc {Guid.NewGuid():N}"[..18], Description = "MLACP-387",
            Format = LoungeShowFormat.Offline, Status = LoungeShowStatus.Cancelled,
            ScheduledStart = DateTimeOffset.UtcNow.AddDays(5), ScheduledEnd = DateTimeOffset.UtcNow.AddDays(5).AddHours(3)
        };
        db.LoungeShows.Add(show);
        await db.SaveChangesAsync();
        var tier = new TicketTier { LoungeShowId = show.Id, Name = "Vào cửa", AccessType = AccessType.Physical };
        db.Add(tier);
        await db.SaveChangesAsync();
        var price = new TicketPrice
        {
            TierId = tier.Id, Name = "Đợt 1", Price = 200_000m,
            PurchaseChannel = method == PaymentMethod.Cash ? PurchaseChannel.Offline : PurchaseChannel.Online,
            SaleStart = DateTimeOffset.UtcNow.AddDays(-200)
        };
        db.Add(price);
        await db.SaveChangesAsync();

        var paidAt = DateTimeOffset.UtcNow.AddDays(-paidDaysAgo);
        var payment = new Payment
        {
            OrderId = $"R387-{Guid.NewGuid():N}"[..30], PayerId = buyerId,
            GrossAmount = 200_000m, PlatformFee = 20_000m, TaxWithheld = 10_000m, NetAmount = 170_000m,
            Method = method, Status = PaymentStatus.Confirmed,
            ReferenceType = method == PaymentMethod.Cash ? "WalkIn" : "TicketHold", ReferenceId = "0",
            PaidAt = paidAt, CreatedAt = paidAt
        };
        db.Add(payment);
        await db.SaveChangesAsync();

        db.Add(new Ticket
        {
            Id = Guid.NewGuid(), BuyerId = buyerId, PriceId = price.Id, TierId = tier.Id, ShowId = show.Id,
            PaymentId = payment.Id, Status = TicketStatus.Cancelled,
            PurchaseChannel = method == PaymentMethod.Cash ? PurchaseChannel.Offline : PurchaseChannel.Online,
            CreatedAt = paidAt
        });
        var refund = new RefundRequest
        {
            PaymentId = payment.Id, RequestedBy = buyerId, Reason = "Event bị hủy — hoàn 100% tiền vé",
            AmountRequested = 200_000m, RefundPercentage = 100m, Status = RefundRequestStatus.Pending
        };
        db.Add(refund);
        await db.SaveChangesAsync();
        return new Seeded(refund.Id, payment.Id, buyerId);
    }

    private Task<HttpResponseMessage> ProvideAsync(int userId, int refundId, bool consent = true)
        => _factory.CreateAuthenticatedClient(userId, "Audience").PutAsJsonAsync(
            $"/api/v1/tickets/refund-requests/{refundId}/payout-account",
            new { BankName = "Vietcombank", AccountNumber, AccountHolder = "NGUYEN VAN A", Consent = consent });

    private Task<HttpResponseMessage> ManualTransferAsync(int refundId, string reference)
        => _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin").PostAsJsonAsync(
            $"/api/v1/admin/refund-requests/{refundId}/process",
            new { Decision = "Approved", ApprovedAmount = (decimal?)null, ManualTransferReference = reference });

    private async Task RunSlaJobAsync()
    {
        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<RefundSlaBreachAlertJob>().ExecuteAsync(new JobCancellationToken(false));
    }

    private async Task<RefundRequest> RefundAsync(int refundId)
    {
        using var scope = _factory.Services.CreateScope();
        return await Db(scope).RefundRequests.AsNoTracking().SingleAsync(r => r.Id == refundId);
    }

    private async Task<int> NoticesAsync(int userId, string title, int refundId)
    {
        using var scope = _factory.Services.CreateScope();
        return await Db(scope).Notifications.AsNoTracking()
            .CountAsync(n => n.UserId == userId && n.Title == title && n.ReferenceId == refundId.ToString());
    }

    // ── Người mua khai tài khoản ─────────────────────────────────────────────

    [Fact]
    public async Task PastTheWindow_TheBuyerCanProvideAnAccount_AndTheirConsentIsRecorded()
    {
        var seeded = await SeedAsync(paidDaysAgo: 120);

        var res = await ProvideAsync(seeded.BuyerId, seeded.RefundId);

        res.StatusCode.Should().Be(HttpStatusCode.NoContent, await res.Content.ReadAsStringAsync());
        var refund = await RefundAsync(seeded.RefundId);
        refund.PayoutBankName.Should().Be("Vietcombank");
        refund.PayoutAccountNumber.Should().Be(AccountNumber);
        refund.PayoutAccountHolder.Should().Be("NGUYEN VAN A");
        refund.PayoutConsentAt.Should().NotBeNull("the consent is the legal basis for paying by a different method");
        refund.Status.Should().Be(RefundRequestStatus.Pending, "providing an account does not approve anything");

        using var scope = _factory.Services.CreateScope();
        (await Db(scope).Notifications.AsNoTracking().AnyAsync(n =>
                n.UserId == SeedHelper.AdminId && n.Title == "Người mua đã khai tài khoản nhận hoàn"
                && n.ReferenceId == seeded.RefundId.ToString()))
            .Should().BeTrue("the Admin who will transfer must know the request is ready");
    }

    [Fact]
    public async Task WithinTheWindow_AnAccountIsRefused_TheMoneyMustGoBackTheWayItCame()
    {
        var seeded = await SeedAsync(paidDaysAgo: 10);

        var res = await ProvideAsync(seeded.BuyerId, seeded.RefundId);

        res.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await RefundAsync(seeded.RefundId)).PayoutConsentAt.Should().BeNull();
    }

    [Fact]
    public async Task SomeoneElse_CannotProvideAnAccountForAnotherBuyersRefund()
    {
        var seeded = await SeedAsync(paidDaysAgo: 120);
        var stranger = await NewUserAsync("x387");

        var res = await ProvideAsync(stranger, seeded.RefundId);

        res.StatusCode.Should().Be(HttpStatusCode.Forbidden, "otherwise anyone could redirect someone else's refund");
        (await RefundAsync(seeded.RefundId)).PayoutAccountNumber.Should().BeNull();
    }

    [Fact]
    public async Task WithoutConsent_TheAccountIsNotAccepted()
    {
        var seeded = await SeedAsync(paidDaysAgo: 120);

        var res = await ProvideAsync(seeded.BuyerId, seeded.RefundId, consent: false);

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await RefundAsync(seeded.RefundId)).PayoutConsentAt.Should().BeNull();
    }

    // ── Admin ghi nhận chuyển khoản ──────────────────────────────────────────

    [Fact]
    public async Task AManualTransfer_WithoutTheBuyersConsent_IsRefused()
    {
        var seeded = await SeedAsync(paidDaysAgo: 120);

        var res = await ManualTransferAsync(seeded.RefundId, "FT26256000001");

        res.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "the Admin must not pick a destination account on the buyer's behalf");
        (await res.Content.ReadAsStringAsync()).Should().Contain("Điều 38");
        (await RefundAsync(seeded.RefundId)).Status.Should().Be(RefundRequestStatus.Pending);
    }

    [Fact]
    public async Task AManualTransfer_AfterConsent_IsRecordedAgainstTheBuyersOwnAccount()
    {
        var seeded = await SeedAsync(paidDaysAgo: 120);
        (await ProvideAsync(seeded.BuyerId, seeded.RefundId)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        const string reference = "FT26256000002";

        var res = await ManualTransferAsync(seeded.RefundId, reference);

        res.StatusCode.Should().Be(HttpStatusCode.NoContent, await res.Content.ReadAsStringAsync());
        var refund = await RefundAsync(seeded.RefundId);
        refund.Status.Should().Be(RefundRequestStatus.Approved);
        refund.ResolutionNote.Should().Contain(reference).And.Contain("6789").And.NotContain(AccountNumber,
            "the note records where the money went without spelling out the full account number");

        using var scope = _factory.Services.CreateScope();
        (await Db(scope).Notifications.AsNoTracking()
                .Where(n => n.UserId == seeded.BuyerId && n.Type == NotificationType.RefundUpdate
                            && n.ReferenceId == seeded.RefundId.ToString())
                .ToListAsync())
            .Should().Contain(n => n.Body.Contains("6789") && n.Body.Contains(reference),
                "the buyer can check the right account received the transfer");
    }

    // ── Nhắc người mua, báo Admin ────────────────────────────────────────────

    [Fact]
    public async Task TheBuyerIsAskedForAnAccount_AndRemindedEveryThreeDays_NotEveryRun()
    {
        var seeded = await SeedAsync(paidDaysAgo: 120);

        await RunSlaJobAsync();
        (await NoticesAsync(seeded.BuyerId, PayoutRequestTitle, seeded.RefundId)).Should().Be(1);

        await RunSlaJobAsync();
        (await NoticesAsync(seeded.BuyerId, PayoutRequestTitle, seeded.RefundId)).Should().Be(1, "the job runs often; the buyer is not spammed");

        using (var scope = _factory.Services.CreateScope())
        {
            var db = Db(scope);
            var refund = await db.RefundRequests.SingleAsync(r => r.Id == seeded.RefundId);
            refund.PayoutAccountRequestedAt = DateTimeOffset.UtcNow.AddDays(-4);
            await db.SaveChangesAsync();
        }

        await RunSlaJobAsync();
        (await NoticesAsync(seeded.BuyerId, PayoutRequestTitle, seeded.RefundId)).Should().Be(2, "three days later the buyer is reminded");
    }

    [Fact]
    public async Task OnceTheBuyerHasConsented_NoMoreRemindersAreSent()
    {
        var seeded = await SeedAsync(paidDaysAgo: 120);
        (await ProvideAsync(seeded.BuyerId, seeded.RefundId)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        await RunSlaJobAsync();

        (await NoticesAsync(seeded.BuyerId, PayoutRequestTitle, seeded.RefundId)).Should().Be(0);
    }

    [Fact]
    public async Task TheVnPayWindowAlert_ReachesEachAdminOnlyOnce()
    {
        var seeded = await SeedAsync(paidDaysAgo: 120);

        await RunSlaJobAsync();
        await RunSlaJobAsync();

        using var scope = _factory.Services.CreateScope();
        var alerts = await Db(scope).Notifications.AsNoTracking()
            .Where(n => n.Title == WindowExpiredTitle && n.ReferenceId == seeded.RefundId.ToString())
            .ToListAsync();
        alerts.Should().NotBeEmpty();
        alerts.GroupBy(n => n.UserId).Should().OnlyContain(g => g.Count() == 1,
            "the same alert every run buries the alerts that matter");
    }

    [Fact]
    public async Task ACashRefund_GetsNoVnPayWindowAlert()
    {
        var seeded = await SeedAsync(paidDaysAgo: 120, PaymentMethod.Cash);

        await RunSlaJobAsync();

        using var scope = _factory.Services.CreateScope();
        (await Db(scope).Notifications.AsNoTracking().AnyAsync(n =>
                (n.Title == WindowExpiredTitle || n.Title == "Sắp hết hạn hoàn tiền qua VNPay")
                && n.ReferenceId == seeded.RefundId.ToString()))
            .Should().BeFalse("cash never went through VNPay, so VNPay's deadline does not apply");
        (await NoticesAsync(seeded.BuyerId, PayoutRequestTitle, seeded.RefundId)).Should().Be(0,
            "the venue hands cash back — the buyer has no account to provide");
    }

    // ── Người mua thấy việc cần làm ──────────────────────────────────────────

    [Fact]
    public async Task MyRefundList_FlagsTheRefundWaitingForAnAccount_UntilItIsProvided()
    {
        var seeded = await SeedAsync(paidDaysAgo: 120);
        var client = _factory.CreateAuthenticatedClient(seeded.BuyerId, "Audience");

        async Task<JsonElement> MineAsync()
        {
            var res = await client.GetAsync("/api/v1/tickets/refund-requests/my");
            res.StatusCode.Should().Be(HttpStatusCode.OK);
            using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
            return doc.RootElement.GetProperty("data").GetProperty("items").EnumerateArray()
                .Single(e => e.GetProperty("id").GetInt32() == seeded.RefundId).Clone();
        }

        (await MineAsync()).GetProperty("payoutAccountRequired").GetBoolean().Should().BeTrue();

        (await ProvideAsync(seeded.BuyerId, seeded.RefundId)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var after = await MineAsync();
        after.GetProperty("payoutAccountRequired").GetBoolean().Should().BeFalse();
        after.GetProperty("payoutBankName").GetString().Should().Be("Vietcombank");
    }

    // ── Xoá dữ liệu cá nhân ──────────────────────────────────────────────────

    [Fact]
    public async Task DataErasure_ClearsTheAccountOnSettledRefunds_ButKeepsItWhereMoneyIsStillOwed()
    {
        var settled = await SeedAsync(paidDaysAgo: 120);
        (await ProvideAsync(settled.BuyerId, settled.RefundId)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await ManualTransferAsync(settled.RefundId, "FT26256000003")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Yeu cau thu hai cua CUNG nguoi mua, van dang cho chuyen khoan.
        var owed = await SeedAsync(paidDaysAgo: 120);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = Db(scope);
            (await db.RefundRequests.SingleAsync(r => r.Id == owed.RefundId)).RequestedBy = settled.BuyerId;
            (await db.Payments.SingleAsync(p => p.Id == owed.PaymentId)).PayerId = settled.BuyerId;
            await db.SaveChangesAsync();
        }
        (await ProvideAsync(settled.BuyerId, owed.RefundId)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var res = await _factory.CreateAuthenticatedClient(settled.BuyerId, "Audience")
            .PostAsJsonAsync("/api/v1/me/data-erasure", new { CurrentPassword = (string?)null });

        res.StatusCode.Should().Be(HttpStatusCode.NoContent, await res.Content.ReadAsStringAsync());
        var settledAfter = await RefundAsync(settled.RefundId);
        settledAfter.PayoutAccountNumber.Should().BeNull("a bank account identifies a natural person");
        settledAfter.PayoutAccountHolder.Should().BeNull();
        settledAfter.ResolutionNote.Should().Contain("6789", "the masked record of where the money went stays as evidence");
        (await RefundAsync(owed.RefundId)).PayoutAccountNumber.Should().Be(AccountNumber,
            "the platform still owes this money — erasing the account would leave no way to pay it");
    }
}
