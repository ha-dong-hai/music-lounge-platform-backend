using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.ValueObjects;
using MusicLounge.Infrastructure.Persistence;
using MusicLounge.Tests.Integration.Helpers;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Tests.Integration.CF6;

/// <summary>
/// MLACP-384. Quá hạn VNPay nhận lệnh hoàn (<c>vnpay_refund_window_days</c>, mặc định 90), <c>ProcessRefundRequest</c>
/// bảo Admin "chuyển khoản thủ công rồi ghi nhận lại" — nhưng không có nơi nào để ghi nhận, nên yêu cầu hoàn nằm
/// Pending vĩnh viễn. Tình huống thật: mua vé sớm hơn 90 ngày rồi buổi diễn bị huỷ hoặc phòng trà bị khoá.
///
/// <para>Chốt 90 ngày còn chặn nhầm cả vé tiền mặt tại quầy — thứ VNPay chưa từng biết tới.</para>
/// </summary>
[Collection("Integration")]
public sealed class ManualRefundPastVnPayWindowTests
{
    private readonly ApiFactory _factory;

    public ManualRefundPastVnPayWindowTests(ApiFactory factory) => _factory = factory;

    private sealed record Seeded(int RefundId, int PaymentId, int OwnerId);

    /// <param name="transactionId">Null cho thanh toán qua cổng thì một lệnh gọi VNPay chắc chắn thất bại
    /// (<c>FakeVnPayService.RefundAsync</c> từ chối khi không có mã giao dịch) — nên duyệt thành công nghĩa là
    /// VNPay KHÔNG bị gọi.</param>
    /// <param name="payoutConsent">MLACP-387: người mua đã tự khai tài khoản và đồng ý nhận hoàn bằng chuyển khoản —
    /// điều kiện để Admin ghi nhận chuyển khoản tay (Luật BVQLNTD 2023 Điều 38 khoản 4).</param>
    private async Task<Seeded> SeedAsync(
        int paidDaysAgo, PaymentMethod method, string? transactionId = null, bool payoutConsent = false)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var owner = new User { Email = $"r384-{Guid.NewGuid():N}@test.com", FullName = "Chủ phòng trà" };
        db.Users.Add(owner);
        await db.SaveChangesAsync();

        var lounge = new MusicLoungeEntity
        {
            OwnerId = owner.Id, Name = $"Venue384-{Guid.NewGuid():N}"[..30], Status = LoungeStatus.Approved,
            Address = new VenueAddress { Street = "1 Test St", District = "1", City = "HCM" }
        };
        db.Add(lounge);
        await db.SaveChangesAsync();

        var show = new LoungeShow
        {
            LoungeId = lounge.Id, Name = $"Đêm nhạc {Guid.NewGuid():N}"[..18], Description = "MLACP-384",
            Format = LoungeShowFormat.Offline, Status = LoungeShowStatus.Cancelled,
            ScheduledStart = DateTimeOffset.UtcNow.AddDays(10), ScheduledEnd = DateTimeOffset.UtcNow.AddDays(10).AddHours(3)
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
            OrderId = $"R384-{Guid.NewGuid():N}"[..30],
            PayerId = method == PaymentMethod.Cash ? null : SeedHelper.AudienceId,
            GrossAmount = 200_000m, PlatformFee = 20_000m, TaxWithheld = 10_000m, NetAmount = 170_000m,
            Method = method, Status = PaymentStatus.Confirmed,
            ReferenceType = method == PaymentMethod.Cash ? "WalkIn" : "TicketHold", ReferenceId = "0",
            TransactionId = transactionId, PaidAt = paidAt, CreatedAt = paidAt
        };
        db.Add(payment);
        await db.SaveChangesAsync();

        db.Add(new Ticket
        {
            Id = Guid.NewGuid(), BuyerId = method == PaymentMethod.Cash ? null : SeedHelper.AudienceId,
            PriceId = price.Id, TierId = tier.Id, ShowId = show.Id, PaymentId = payment.Id,
            Status = TicketStatus.Cancelled,
            PurchaseChannel = method == PaymentMethod.Cash ? PurchaseChannel.Offline : PurchaseChannel.Online,
            CreatedAt = paidAt
        });

        var refund = new RefundRequest
        {
            PaymentId = payment.Id, RequestedBy = method == PaymentMethod.Cash ? null : SeedHelper.AudienceId,
            Reason = "Event bị hủy — hoàn 100% tiền vé", AmountRequested = 200_000m, RefundPercentage = 100m,
            Status = RefundRequestStatus.Pending,
            PayoutBankName = payoutConsent ? "Vietcombank" : null,
            PayoutAccountNumber = payoutConsent ? "0123456789" : null,
            PayoutAccountHolder = payoutConsent ? "NGUYEN VAN A" : null,
            PayoutConsentAt = payoutConsent ? DateTimeOffset.UtcNow : null
        };
        db.Add(refund);
        await db.SaveChangesAsync();

        return new Seeded(refund.Id, payment.Id, owner.Id);
    }

    private Task<HttpResponseMessage> ProcessAsync(int refundId, string decision, string? manualTransferReference)
        => _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin").PostAsJsonAsync(
            $"/api/v1/admin/refund-requests/{refundId}/process",
            new { Decision = decision, ApprovedAmount = (decimal?)null, ManualTransferReference = manualTransferReference });

    private async Task<RefundRequest> RefundAsync(int refundId)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().RefundRequests.AsNoTracking()
            .SingleAsync(r => r.Id == refundId);
    }

    [Fact]
    public async Task PastWindow_WithoutAReference_IsRefused_AndTellsTheAdminHowToRecordIt()
    {
        var seeded = await SeedAsync(paidDaysAgo: 120, PaymentMethod.Gateway, transactionId: "V120DAYSAGO");

        var res = await ProcessAsync(seeded.RefundId, "Approved", manualTransferReference: null);

        res.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await res.Content.ReadAsStringAsync()).Should().Contain("ManualTransferReference",
            "the message must name the way to close this, now that there is one");
        (await RefundAsync(seeded.RefundId)).Status.Should().Be(RefundRequestStatus.Pending);
    }

    [Fact]
    public async Task PastWindow_WithAReference_ClosesTheRefund_WithoutCallingVnPay()
    {
        // MLACP-387: chuyển khoản tay chỉ được ghi nhận khi người mua đã đồng ý và khai tài khoản.
        var seeded = await SeedAsync(paidDaysAgo: 120, PaymentMethod.Gateway, transactionId: null, payoutConsent: true);
        const string reference = "FT26255123456";

        var res = await ProcessAsync(seeded.RefundId, "Approved", reference);

        res.StatusCode.Should().Be(HttpStatusCode.NoContent,
            "the Admin already moved the money by bank transfer — " + await res.Content.ReadAsStringAsync());

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var refund = await db.RefundRequests.AsNoTracking().SingleAsync(r => r.Id == seeded.RefundId);
        refund.Status.Should().Be(RefundRequestStatus.Approved);
        refund.AmountApproved.Should().Be(200_000m);
        refund.ResolutionNote.Should().Contain(reference, "the transfer is the only proof the buyer was paid");
        (await db.Payments.AsNoTracking().SingleAsync(p => p.Id == seeded.PaymentId)).Status
            .Should().Be(PaymentStatus.Refunded);
        (await db.LedgerEntries.AsNoTracking()
                .Where(e => e.PaymentId == seeded.PaymentId && !e.IsDebit).ToListAsync())
            .Should().Contain(e => e.Description != null && e.Description.Contains(reference),
                "the reversal must say the money left by bank transfer, not through the gateway");
        (await db.Notifications.AsNoTracking()
                .Where(n => n.UserId == SeedHelper.AudienceId && n.Type == NotificationType.RefundUpdate
                            && n.ReferenceId == seeded.RefundId.ToString())
                .ToListAsync())
            .Should().Contain(n => n.Body.Contains(reference) && n.Body.Contains("chuyen khoan"),
                "the buyer is told the truth: a transfer, with a reference they can chase");
    }

    [Fact]
    public async Task WithinWindow_AReferenceIsRefused_TheMoneyMustGoBackThroughVnPay()
    {
        var seeded = await SeedAsync(paidDaysAgo: 10, PaymentMethod.Gateway, transactionId: "V10DAYSAGO");

        var res = await ProcessAsync(seeded.RefundId, "Approved", "FT26255999999");

        res.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "a bypass of the gateway while it can still reverse the charge would leave nothing to reconcile against");
        (await RefundAsync(seeded.RefundId)).Status.Should().Be(RefundRequestStatus.Pending);
    }

    [Fact]
    public async Task CashAtTheDoor_PastNinetyDays_CanStillBeApproved()
    {
        var seeded = await SeedAsync(paidDaysAgo: 120, PaymentMethod.Cash);

        var res = await ProcessAsync(seeded.RefundId, "Approved", manualTransferReference: null);

        res.StatusCode.Should().Be(HttpStatusCode.NoContent,
            "VNPay's window is VNPay's — cash never touched it — " + await res.Content.ReadAsStringAsync());
        (await RefundAsync(seeded.RefundId)).Status.Should().Be(RefundRequestStatus.Approved);

        using var scope = _factory.Services.CreateScope();
        (await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Notifications.AsNoTracking()
                .AnyAsync(n => n.UserId == seeded.OwnerId && n.Type == NotificationType.RefundOwedByVenue
                               && n.ReferenceId == seeded.RefundId.ToString()))
            .Should().BeTrue("the venue holds this cash and is told to hand it back");
    }

    [Fact]
    public async Task CashAtTheDoor_AReferenceIsRefused()
    {
        var seeded = await SeedAsync(paidDaysAgo: 120, PaymentMethod.Cash);

        var res = await ProcessAsync(seeded.RefundId, "Approved", "FT26255000001");

        res.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, "there is no transfer to record for cash");
        (await RefundAsync(seeded.RefundId)).Status.Should().Be(RefundRequestStatus.Pending);
    }

    [Fact]
    public async Task Rejecting_WithAReference_IsAValidationError()
    {
        var seeded = await SeedAsync(paidDaysAgo: 120, PaymentMethod.Gateway, transactionId: "V120B");

        var res = await ProcessAsync(seeded.RefundId, "Rejected", "FT26255000002");

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest, "a rejected refund moves no money to record");
        (await RefundAsync(seeded.RefundId)).Status.Should().Be(RefundRequestStatus.Pending);
    }
}
