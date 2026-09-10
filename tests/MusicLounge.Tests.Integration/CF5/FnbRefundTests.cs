using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Infrastructure.Jobs;
using MusicLounge.Infrastructure.Persistence;
using MusicLounge.Tests.Integration.Helpers;

namespace MusicLounge.Tests.Integration.CF5;

/// <summary>
/// MLACP-351. Trước task này không có đường hoàn tiền nào cho thanh toán F&amp;B online:
/// <list type="bullet">
///   <item><c>ProcessRefundRequest</c> tìm chủ phòng trà qua <b>vé</b> — thanh toán F&amp;B không có vé
///   nên mọi yêu cầu hoàn đều dừng ở "không xác định được chủ phòng trà".</item>
///   <item>Đơn khách đã trả trước mà phòng trà không phục vụ được thì không huỷ được (MLACP-349 tạm chặn
///   vì chưa có đường hoàn).</item>
///   <item>Khoản trả trùng / tiền về cho đơn đã huỷ chỉ được ghi nhận và báo Admin.</item>
/// </list>
/// </summary>
[Collection("Integration")]
public sealed class FnbRefundTests
{
    private const decimal ItemPrice = 45_000m;
    private const int Quantity = 2;
    private const decimal OrderTotal = ItemPrice * Quantity;

    private readonly ApiFactory _factory;

    public FnbRefundTests(ApiFactory factory) => _factory = factory;

    private sealed record DataResponse<T>(bool Success, T Data);
    private sealed record PaymentInit(int OrderId, string PaymentGatewayOrderId, decimal Amount, string PaymentUrl);
    private sealed record IpnBody(string RspCode, string Message);

    private HttpClient Audience() => _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience");
    private HttpClient Staff() => _factory.CreateAuthenticatedClient(SeedHelper.StaffId, "Staff", SeedHelper.LoungeId);
    private HttpClient Admin() => _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin");

    private async Task<int> CreateOrderAsync()
    {
        int menuItemId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var menu = new FnbMenu
            {
                LoungeId = SeedHelper.LoungeId, Name = "Menu MLACP-351", IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            db.Add(menu);
            await db.SaveChangesAsync();
            var item = new FnbMenuItem
            {
                MenuId = menu.Id, Category = "Drink", Name = "Nước ép cam", Price = ItemPrice, IsAvailable = true
            };
            db.Add(item);
            await db.SaveChangesAsync();
            menuItemId = item.Id;
        }

        var res = await Audience().PostAsJsonAsync("/api/v1/fnb-orders", new
        {
            LoungeId = SeedHelper.LoungeId,
            ShowId = (int?)null,
            ZoneId = (int?)null,
            TableNote = "Bàn D4",
            PaymentMethod = "Cash",
            Note = (string?)null,
            Items = new[] { new { MenuItemId = menuItemId, Quantity, Note = (string?)null } }
        });
        res.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await res.Content.ReadFromJsonAsync<DataResponse<int>>())!.Data;
    }

    private async Task<string> InitiateAsync(int orderId)
    {
        var res = await Audience().PostAsync($"/api/v1/fnb-orders/{orderId}/pay", null);
        res.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await res.Content.ReadFromJsonAsync<DataResponse<PaymentInit>>())!.Data.PaymentGatewayOrderId;
    }

    private async Task<string> IpnAsync(string txnRef)
    {
        var transactionNo = $"R{Guid.NewGuid():N}"[..14];
        var url = $"/api/v1/fnb-orders/vnpay-ipn?vnp_TxnRef={txnRef}&vnp_ResponseCode=00" +
                  $"&vnp_Amount={(long)(OrderTotal * 100)}&vnp_TransactionNo={transactionNo}";
        var res = await _factory.CreateClient().GetAsync(url);
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await res.Content.ReadFromJsonAsync<IpnBody>())!.RspCode;
    }

    private Task<HttpResponseMessage> StaffSetAsync(int orderId, string status)
        => Staff().PutAsJsonAsync($"/api/v1/fnb-orders/{orderId}/status", new { Status = status });

    private async Task<Payment> PaymentAsync(string txnRef)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Payments.AsNoTracking().SingleAsync(p => p.OrderId == txnRef);
    }

    private async Task<List<RefundRequest>> RefundsAsync(int paymentId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.RefundRequests.AsNoTracking().Where(r => r.PaymentId == paymentId).ToListAsync();
    }

    private async Task<(decimal PlatformDebit, decimal OwnerDebit, decimal GatewayCredit, int Lines)> RefundJournalAsync(int paymentId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var rows = await db.LedgerEntries.AsNoTracking()
            .Where(e => e.PaymentId == paymentId && e.ReferenceType == "refund")
            .Select(e => new { e.Account.OwnerType, e.Account.OwnerId, e.Amount, e.IsDebit })
            .ToListAsync();
        return (
            rows.Where(r => r.OwnerType == AccountType.Platform && r.IsDebit).Sum(r => r.Amount),
            rows.Where(r => r.OwnerType == AccountType.User && r.OwnerId == SeedHelper.OwnerId && r.IsDebit).Sum(r => r.Amount),
            rows.Where(r => r.OwnerType == AccountType.Gateway && !r.IsDebit).Sum(r => r.Amount),
            rows.Count);
    }

    private Task<HttpResponseMessage> ApproveAsync(int refundId)
        => Admin().PostAsJsonAsync($"/api/v1/admin/refund-requests/{refundId}/process", new { Decision = "Approved" });

    // ── Phòng trà huỷ đơn khách đã trả trước ────────────────────────────────

    [Fact]
    public async Task PhongTraHuyDonDaTraTruocThiTuTaoYeuCauHoan100()
    {
        var orderId = await CreateOrderAsync();
        var txnRef = await InitiateAsync(orderId);
        (await IpnAsync(txnRef)).Should().Be("00");

        (await StaffSetAsync(orderId, "Cancelled")).StatusCode.Should().Be(HttpStatusCode.NoContent,
            "hết món sau khi khách đã trả — trước đây bị chặn 422 vì chưa có đường hoàn");

        var payment = await PaymentAsync(txnRef);
        var refund = (await RefundsAsync(payment.Id)).Should().ContainSingle().Subject;
        refund.RefundPercentage.Should().Be(100m, "món chưa giao thì tiền phải về lại khách");
        refund.AmountRequested.Should().Be(OrderTotal);
        refund.Status.Should().Be(RefundRequestStatus.Pending);
        refund.RequestedBy.Should().Be(SeedHelper.AudienceId);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.Notifications.AnyAsync(n =>
                n.UserId == SeedHelper.AudienceId
                && n.Title == "Đơn F&B đã bị hủy — bạn sẽ được hoàn tiền"
                && n.ReferenceId == orderId.ToString()))
            .Should().BeTrue("khách phải biết tiền của mình đi đâu, không chỉ nghe 'đã bị huỷ'");
    }

    [Fact]
    public async Task DuyetHoanThiHoanQuaVnPayVaTruTuKhoanPlatformDangGiu()
    {
        var orderId = await CreateOrderAsync();
        var txnRef = await InitiateAsync(orderId);
        await IpnAsync(txnRef);
        (await StaffSetAsync(orderId, "Cancelled")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        var payment = await PaymentAsync(txnRef);
        var refund = (await RefundsAsync(payment.Id)).Single();

        (await ApproveAsync(refund.Id)).StatusCode.Should().Be(HttpStatusCode.NoContent,
            "trước đây: 422 'Không xác định được chủ phòng trà' — handler chỉ biết tìm chủ qua vé");

        (await PaymentAsync(txnRef)).Status.Should().Be(PaymentStatus.Refunded);

        var journal = await RefundJournalAsync(payment.Id);
        journal.PlatformDebit.Should().Be(OrderTotal, "tiền đang được giữ hộ ở Platform (MLACP-350)");
        journal.OwnerDebit.Should().Be(0m, "chưa giải ngân đồng nào cho phòng trà — không có gì để thu hồi");
        journal.GatewayCredit.Should().Be(OrderTotal);
    }

    [Fact]
    public async Task DonDaPhucVuXongKhongHuyDuoc()
    {
        var orderId = await CreateOrderAsync();
        await IpnAsync(await InitiateAsync(orderId));
        (await StaffSetAsync(orderId, "Preparing")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await StaffSetAsync(orderId, "Served")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await StaffSetAsync(orderId, "Cancelled")).StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "món đã giao — huỷ ngang không phải đường cho việc này");
    }

    // ── Khoản VNPay đã thu nhưng không áp vào đơn ───────────────────────────

    [Fact]
    public async Task KhoanTraTrungTuTaoYeuCauHoanVaKhongDaoButToanChuaTungGhi()
    {
        var orderId = await CreateOrderAsync();
        var first = await InitiateAsync(orderId);
        var second = await InitiateAsync(orderId);
        await IpnAsync(first);
        (await IpnAsync(second)).Should().Be("02");

        var duplicate = await PaymentAsync(second);
        var refund = (await RefundsAsync(duplicate.Id)).Should().ContainSingle(
            "tiền trả trùng là tiền của khách — không có gì để tranh cãi về số tiền").Subject;
        refund.RefundPercentage.Should().Be(100m);
        refund.AmountRequested.Should().Be(OrderTotal);

        (await ApproveAsync(refund.Id)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await PaymentAsync(second)).Status.Should().Be(PaymentStatus.Refunded);

        (await RefundJournalAsync(duplicate.Id)).Lines.Should().Be(0,
            "khoản này chưa từng được ghi sổ — đảo nó là trừ Platform một khoản Platform chưa từng giữ");
    }

    [Fact]
    public async Task TienVeChoDonDaHuyTuTaoYeuCauHoan()
    {
        var orderId = await CreateOrderAsync();
        var txnRef = await InitiateAsync(orderId);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var pending = await db.Payments.SingleAsync(p => p.OrderId == txnRef);
            pending.CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-20); // link đã hết hạn → huỷ được
            await db.SaveChangesAsync();
        }
        (await StaffSetAsync(orderId, "Cancelled")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await IpnAsync(txnRef)).Should().Be("02");

        (await RefundsAsync((await PaymentAsync(txnRef)).Id)).Should().ContainSingle();
    }

    // ── Thanh toán ghi sổ kiểu cũ ───────────────────────────────────────────

    [Fact]
    public async Task ThanhToanKieuCuThuHoiTuTaiKhoanChuPhongTraChuKhongTuPlatform()
    {
        // Trước MLACP-350 bút toán F&B ghi Có THẲNG cho chủ phòng trà. Platform chưa từng giữ khoản đó,
        // nên trừ Platform khi hoàn là trừ nhầm chỗ.
        var orderId = await CreateOrderAsync();
        (await StaffSetAsync(orderId, "Preparing")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        int paymentId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var ledger = scope.ServiceProvider.GetRequiredService<ILedgerService>();
            var payment = new Payment
            {
                OrderId = $"FNB-LEGACY-{Guid.NewGuid():N}"[..40],
                PayerId = SeedHelper.AudienceId,
                GrossAmount = OrderTotal,
                NetAmount = OrderTotal,
                Method = PaymentMethod.Gateway,
                Status = PaymentStatus.Confirmed,
                TransactionId = $"K{Guid.NewGuid():N}"[..14],
                ReferenceType = "FnbOrder",
                ReferenceId = orderId.ToString(),
                PaidAt = DateTimeOffset.UtcNow.AddHours(-2),
                CreatedAt = DateTimeOffset.UtcNow.AddHours(-2)
            };
            db.Add(payment);
            await db.SaveChangesAsync();
            await ledger.WriteJournalAsync(
                Guid.NewGuid().ToString("N"), "fnb_order", orderId.ToString(), payment.Id,
                new LedgerLine[]
                {
                    new(AccountType.Gateway, null, OrderTotal, IsDebit: true),
                    new(AccountType.User, SeedHelper.OwnerId, OrderTotal, IsDebit: false)
                });
            await db.SaveChangesAsync();
            paymentId = payment.Id;
        }

        (await StaffSetAsync(orderId, "Cancelled")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        var refund = (await RefundsAsync(paymentId)).Single();
        (await ApproveAsync(refund.Id)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var journal = await RefundJournalAsync(paymentId);
        journal.OwnerDebit.Should().Be(OrderTotal, "tiền đang nằm ở tài khoản của chủ phòng trà trên sổ");
        journal.PlatformDebit.Should().Be(0m);
    }

    // ── Đi qua đúng luồng duyệt hoàn sẵn có ─────────────────────────────────

    [Fact]
    public async Task QuaHanMaChuaAiDuyetThiHeThongTuDuyetNhuMoiYeuCauHoanKhac()
    {
        var orderId = await CreateOrderAsync();
        var txnRef = await InitiateAsync(orderId);
        await IpnAsync(txnRef);
        (await StaffSetAsync(orderId, "Cancelled")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        var refundId = (await RefundsAsync((await PaymentAsync(txnRef)).Id)).Single().Id;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var refund = await db.RefundRequests.SingleAsync(r => r.Id == refundId);
            refund.CreatedAt = DateTime.UtcNow.AddHours(-100); // quá 72h SLA + 24h ân hạn
            await db.SaveChangesAsync();
        }

        using (var scope = _factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<AutoApproveOverdueRefundsJob>()
                .ExecuteAsync(new JobCancellationToken(false));
        }

        (await RefundsAsync((await PaymentAsync(txnRef)).Id)).Single().Status.Should().Be(RefundRequestStatus.Approved,
            "trước đây handler dừng ở bước tìm chủ phòng trà nên job tự duyệt cũng không duyệt được yêu cầu F&B nào");
    }
}
