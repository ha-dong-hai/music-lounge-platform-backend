using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Infrastructure.Persistence;
using MusicLounge.Tests.Integration.Helpers;

namespace MusicLounge.Tests.Integration.Payments;

/// <summary>
/// MLACP-337. Hai lỗi trong cùng một handler, cùng gây hại cho người mua.
///
/// <para><b>1. Vé mua tại quầy không bao giờ hoàn tiền được.</b>
/// <c>ProcessRefundRequestCommandHandler</c> gọi <c>_vnPay.RefundAsync(...)</c> mà không kiểm
/// <c>payment.Method</c>. Vé bán tại quầy có <c>Method = Cash</c>, <c>TransactionId</c> null, và
/// <c>OrderId</c> chưa bao giờ được gửi sang VNPay — nên lệnh gọi thất bại, handler ném
/// <c>ExternalServiceException</c>, và yêu cầu hoàn tiền nằm <c>Pending</c> vĩnh viễn.</para>
///
/// <para>Khối đảo bút toán cũng sẽ ghi một dòng có <c>AccountType.Gateway</c> — "hoàn tiền qua cổng
/// thanh toán" — cho một giao dịch chưa bao giờ đi qua cổng nào. Vé tiền mặt <b>không ghi sổ cái</b>
/// (<c>WriteTicketLedgerHandler</c> bỏ qua <c>Cash</c>), nên bút toán đảo đó không đối ứng với gì
/// cả. Sổ cái chỉ ghi thêm chứ không sửa được.</para>
///
/// <para><b>2. Mọi quyết định hoàn tiền đều im lặng.</b> Handler không báo cho ai, và không tồn tại
/// <c>NotificationType</c> nào cho việc hoàn tiền được duyệt hay bị từ chối. Người mua gửi yêu cầu
/// rồi phải tự đi hỏi.</para>
/// </summary>
[Collection("Integration")]
public sealed class CashRefundTests
{
    private readonly ApiFactory _factory;

    public CashRefundTests(ApiFactory factory) => _factory = factory;

    /// <param name="method">
    /// <c>Cash</c> tái hiện vé bán tại quầy: không <c>TransactionId</c>, không bút toán, không
    /// settlement — đúng như <c>SellWalkInTicket</c> tạo ra.
    /// </param>
    private async Task<(int PaymentId, int RefundId)> PurchaseWithRefundRequestAsync(
        PaymentMethod method, decimal gross = 200_000m)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var payment = new Payment
        {
            OrderId = $"MLACP337-{Guid.NewGuid():N}"[..30],
            PayerId = SeedHelper.AudienceId,
            GrossAmount = gross,
            NetAmount = gross,
            Method = method,
            Status = PaymentStatus.Confirmed,
            ReferenceType = method == PaymentMethod.Cash ? "WalkIn" : "TicketHold",
            ReferenceId = "0",
            TransactionId = method == PaymentMethod.Gateway
                ? $"G{Guid.NewGuid():N}"[..16]
                : null,
            PaidAt = DateTimeOffset.UtcNow.AddHours(-2),
            CreatedAt = DateTimeOffset.UtcNow.AddHours(-2)
        };
        db.Add(payment);
        await db.SaveChangesAsync();

        db.Add(new Ticket
        {
            Id = Guid.NewGuid(),
            BuyerId = SeedHelper.AudienceId,
            PriceId = SeedHelper.TicketPriceId,
            TierId = SeedHelper.TicketTierId,
            ShowId = SeedHelper.ShowId,
            PaymentId = payment.Id,
            Status = TicketStatus.Confirmed,
            PurchaseChannel = method == PaymentMethod.Cash
                ? PurchaseChannel.Offline
                : PurchaseChannel.Online,
            CreatedAt = DateTimeOffset.UtcNow
        });

        var refund = new RefundRequest
        {
            PaymentId = payment.Id,
            RequestedBy = SeedHelper.AudienceId,
            Reason = "Buoi dien khong dien ra",
            AmountRequested = gross,
            RefundPercentage = 100m,
            Status = RefundRequestStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
        db.Add(refund);
        await db.SaveChangesAsync();

        return (payment.Id, refund.Id);
    }

    private Task<HttpResponseMessage> ProcessAsync(int refundId, string decision, decimal? amount)
        => _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin")
            .PostAsJsonAsync($"/api/v1/admin/refund-requests/{refundId}/process",
                new { Decision = decision, ApprovedAmount = amount });

    private async Task<T> QueryAsync<T>(Func<ApplicationDbContext, Task<T>> read)
    {
        using var scope = _factory.Services.CreateScope();
        return await read(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
    }

    // ── Vé mua tại quầy ─────────────────────────────────────────────────────

    [Fact]
    public async Task VeMuaTaiQuayPhaiHoanTienDuoc()
    {
        var (_, refundId) = await PurchaseWithRefundRequestAsync(PaymentMethod.Cash);

        var res = await ProcessAsync(refundId, "Approved", 200_000m);

        res.StatusCode.Should().Be(HttpStatusCode.NoContent,
            "gọi VNPay cho một giao dịch tiền mặt sẽ thất bại và để yêu cầu kẹt Pending vĩnh viễn");

        (await QueryAsync(db => db.RefundRequests.FindAsync(refundId).AsTask()))!
            .Status.Should().Be(RefundRequestStatus.Approved);
    }

    [Fact]
    public async Task VeMuaTaiQuayKhongDuocGhiButToanDao()
    {
        var (paymentId, refundId) = await PurchaseWithRefundRequestAsync(PaymentMethod.Cash);

        await ProcessAsync(refundId, "Approved", 200_000m);

        var entries = await QueryAsync(db =>
            db.LedgerEntries.CountAsync(e => e.PaymentId == paymentId));

        entries.Should().Be(0,
            "vé tiền mặt chưa bao giờ ghi bút toán mua, nên một bút toán đảo sẽ không đối ứng với " +
            "gì cả — trong đó có một dòng ghi có cho cổng thanh toán chưa từng nhận đồng nào");
    }

    [Fact]
    public async Task VeMuaTaiQuayThiPhongTraPhaiDuocBaoLaHoPhaiTraTienMat()
    {
        var (_, refundId) = await PurchaseWithRefundRequestAsync(PaymentMethod.Cash);

        await ProcessAsync(refundId, "Approved", 200_000m);

        var told = await QueryAsync(db => db.Notifications.AnyAsync(n =>
            n.UserId == SeedHelper.OwnerId
            && n.Type == NotificationType.RefundOwedByVenue
            && n.ReferenceId == refundId.ToString()));

        told.Should().BeTrue(
            "nền tảng chưa bao giờ giữ khoản này nên không trả thay được — phòng trà phải biết " +
            "chính họ nợ khách số tiền đó");
    }

    // ── Vé mua online vẫn phải đi đúng đường cũ ─────────────────────────────

    [Fact]
    public async Task VeMuaOnlineVanGhiButToanDaoNhuCu()
    {
        // Chiều đối chứng: tách nhánh tiền mặt ra không được làm hỏng đường thanh toán online.
        var (paymentId, refundId) = await PurchaseWithRefundRequestAsync(PaymentMethod.Gateway);

        // Bút toán mua — để có thứ mà đảo. Ghi qua chính ILedgerService như production thay vì tự
        // dựng LedgerEntry, để không phải tự giải AccountId và không lệch với cách thật.
        using (var scope = _factory.Services.CreateScope())
        {
            var ledger = scope.ServiceProvider.GetRequiredService<ILedgerService>();
            await ledger.WriteJournalAsync(
                Guid.NewGuid().ToString("N"), LedgerReferenceTypes.Payment, paymentId.ToString(), paymentId,
                [
                    new LedgerLine(AccountType.Gateway, null, 200_000m, IsDebit: true, Description: "Mua ve"),
                    new LedgerLine(AccountType.Platform, null, 200_000m, IsDebit: false, Description: "Mua ve")
                ]);
            await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().SaveChangesAsync();
        }

        var res = await ProcessAsync(refundId, "Approved", 200_000m);
        res.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var entries = await QueryAsync(db =>
            db.LedgerEntries.CountAsync(e => e.PaymentId == paymentId));

        entries.Should().BeGreaterThan(2, "đường online vẫn phải ghi bút toán đảo như trước");
    }

    // ── Người mua phải được biết kết quả ────────────────────────────────────

    [Fact]
    public async Task NguoiMuaPhaiDuocBaoKhiYeuCauDuocDuyet()
    {
        var (_, refundId) = await PurchaseWithRefundRequestAsync(PaymentMethod.Cash);

        await ProcessAsync(refundId, "Approved", 200_000m);

        var told = await QueryAsync(db => db.Notifications.AnyAsync(n =>
            n.UserId == SeedHelper.AudienceId
            && n.Type == NotificationType.RefundUpdate
            && n.ReferenceId == refundId.ToString()));

        told.Should().BeTrue("gửi yêu cầu rồi phải tự đi hỏi kết quả là không chấp nhận được");
    }

    [Fact]
    public async Task NguoiMuaPhaiDuocBaoCaKhiYeuCauBiTuChoi()
    {
        var (_, refundId) = await PurchaseWithRefundRequestAsync(PaymentMethod.Cash);

        await ProcessAsync(refundId, "Rejected", null);

        var told = await QueryAsync(db => db.Notifications.AnyAsync(n =>
            n.UserId == SeedHelper.AudienceId
            && n.Type == NotificationType.RefundUpdate
            && n.ReferenceId == refundId.ToString()));

        told.Should().BeTrue(
            "từ chối trong im lặng còn tệ hơn từ chối — người mua không biết để khiếu nại tiếp");
    }
}
