using FluentAssertions;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Infrastructure.Jobs;
using MusicLounge.Infrastructure.Persistence;
using MusicLounge.Tests.Integration.Fakes;
using MusicLounge.Tests.Integration.Helpers;

namespace MusicLounge.Tests.Integration.Payments;

/// <summary>
/// MLACP-343. Trước task này hệ thống <b>chỉ biết về thanh toán qua callback</b>. Một callback mất
/// hẳn — endpoint chết suốt cả cửa sổ retry, hoặc VNPay bỏ cuộc — là tiền khách đã trả mà hệ thống
/// đánh <c>Failed</c> và huỷ vé, trong khi <b>không một chỗ nào đọc <c>PaymentStatus.Failed</c></b>
/// (5 chỗ ghi, 0 chỗ đọc). Tiền mất không dấu vết.
///
/// <para>MLACP-333 (nới cửa sổ huỷ) và MLACP-334 (xử lý callback đến muộn) đều chỉ giúp khi callback
/// CÓ đến. Đây là lỗ hổng còn lại.</para>
///
/// <para><b>Chi tiết dễ sai trong spec:</b> phản hồi querydr có hai trường khác nhau.
/// <c>vnp_ResponseCode</c> = "00" chỉ nghĩa là <b>lệnh truy vấn chạy được</b>;
/// <c>vnp_TransactionStatus</c> = "00" mới nghĩa là <b>giao dịch đã thanh toán thành công</b>. Một
/// truy vấn chạy được cho một giao dịch thất bại vẫn có <c>ResponseCode = "00"</c>.</para>
/// </summary>
[Collection("Integration")]
public sealed class VnPayReconciliationTests
{
    private readonly ApiFactory _factory;

    public VnPayReconciliationTests(ApiFactory factory) => _factory = factory;

    private async Task<(int PaymentId, string TxnRef, Guid TicketId)> StalePendingPurchaseAsync(
        PaymentMethod method = PaymentMethod.Gateway)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var payment = new Payment
        {
            OrderId = $"MLACP343-{Guid.NewGuid():N}"[..30],
            PayerId = SeedHelper.AudienceId,
            GrossAmount = 400_000m,
            NetAmount = 400_000m,
            Method = method,
            Status = PaymentStatus.Pending,
            ReferenceType = method == PaymentMethod.Cash ? "WalkIn" : "TicketHold",
            ReferenceId = "0",
            CreatedAt = DateTimeOffset.UtcNow.AddHours(-3)   // quá cửa sổ 60 phút
        };
        db.Add(payment);
        await db.SaveChangesAsync();

        var ticket = new Ticket
        {
            Id = Guid.NewGuid(),
            BuyerId = SeedHelper.AudienceId,
            PriceId = SeedHelper.TicketPriceId,
            TierId = SeedHelper.TicketTierId,
            ShowId = SeedHelper.ShowId,
            PaymentId = payment.Id,
            Status = TicketStatus.Pending,
            PurchaseChannel = PurchaseChannel.Online,
            CreatedAt = DateTimeOffset.UtcNow.AddHours(-3)
        };
        db.Add(ticket);
        await db.SaveChangesAsync();

        return (payment.Id, payment.OrderId, ticket.Id);
    }

    private async Task RunJobAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var job = scope.ServiceProvider.GetRequiredService<CancelAbandonedPaymentsJob>();
        await job.ExecuteAsync(new JobCancellationToken(false));
    }

    private async Task<(PaymentStatus Payment, TicketStatus Ticket)> StateAsync(
        int paymentId, Guid ticketId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return ((await db.Payments.FindAsync(paymentId))!.Status,
                (await db.Tickets.FindAsync(ticketId))!.Status);
    }

    private async Task<int> AlertCountAsync(int paymentId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Notifications.CountAsync(n =>
            n.UserId == SeedHelper.AdminId
            && n.Type == NotificationType.PaymentConfirmedAfterExpiry
            && n.ReferenceType == "payment"
            && n.ReferenceId == paymentId.ToString());
    }

    // ── VNPay nói đã trả tiền: không được huỷ vé ────────────────────────────

    [Fact]
    public async Task VnPayXacNhanDaTraTienThiKhongDuocHuyVe()
    {
        var (paymentId, txnRef, ticketId) = await StalePendingPurchaseAsync();
        FakeVnPayService.SayPaid(txnRef);
        try
        {
            await RunJobAsync();

            var (payment, ticket) = await StateAsync(paymentId, ticketId);
            payment.Should().Be(PaymentStatus.Pending,
                "huỷ lúc này là lấy tiền của khách mà không đưa gì");
            ticket.Should().Be(TicketStatus.Pending);
        }
        finally { FakeVnPayService.Forget(txnRef); }
    }

    [Fact]
    public async Task PhaiBaoAdminVaChiBaoMotLan()
    {
        var (paymentId, txnRef, _) = await StalePendingPurchaseAsync();
        FakeVnPayService.SayPaid(txnRef);
        try
        {
            await RunJobAsync();
            await RunJobAsync();
            await RunJobAsync();

            (await AlertCountAsync(paymentId)).Should().Be(1,
                "job chạy mỗi phút và thanh toán này vẫn ở Pending — thiếu chốt chống trùng là báo " +
                "mỗi phút một lần");
        }
        finally { FakeVnPayService.Forget(txnRef); }
    }

    // ── VNPay nói chưa trả tiền: huỷ như cũ ─────────────────────────────────

    [Fact]
    public async Task VnPayXacNhanCHUATraTienThiVanHuyNhuCu()
    {
        var (paymentId, txnRef, ticketId) = await StalePendingPurchaseAsync();
        FakeVnPayService.SayNotPaid(txnRef);
        try
        {
            await RunJobAsync();

            var (payment, ticket) = await StateAsync(paymentId, ticketId);
            payment.Should().Be(PaymentStatus.Failed);
            ticket.Should().Be(TicketStatus.Cancelled);
            (await AlertCountAsync(paymentId)).Should().Be(0, "không có sự cố nào để báo");
        }
        finally { FakeVnPayService.Forget(txnRef); }
    }

    // ── Không hỏi được: giữ nguyên hành vi cũ ───────────────────────────────

    [Fact]
    public async Task KhongDoiSoatDuocThiVanHuyChuKhongTreoVinhVien()
    {
        // VNPay khoá merchant API trên tài khoản sandbox theo mặc định. Nếu không hỏi được mà cũng
        // không huỷ thì job trở nên vô dụng: chỗ ngồi bị giữ vĩnh viễn và người mua thật không còn
        // vé để mua. Bảo vệ được khi API có sẵn, và không làm hỏng hệ thống khi không có.
        var (paymentId, _, ticketId) = await StalePendingPurchaseAsync();

        await RunJobAsync();

        var (payment, ticket) = await StateAsync(paymentId, ticketId);
        payment.Should().Be(PaymentStatus.Failed);
        ticket.Should().Be(TicketStatus.Cancelled);
    }

    [Fact]
    public async Task VeBanTaiQuayKhongDiHoiVnPay()
    {
        // Vé bán tại quầy thu tiền mặt — VNPay chưa bao giờ biết đến chúng (MLACP-337). Hỏi là
        // chắc chắn nhận về "không tìm thấy giao dịch", tốn một lệnh gọi cho một câu trả lời vô nghĩa.
        var (paymentId, txnRef, ticketId) = await StalePendingPurchaseAsync(PaymentMethod.Cash);
        FakeVnPayService.SayPaid(txnRef);   // dù có đặt sẵn câu trả lời, đường này không hỏi
        try
        {
            await RunJobAsync();

            var (payment, ticket) = await StateAsync(paymentId, ticketId);
            payment.Should().Be(PaymentStatus.Failed);
            ticket.Should().Be(TicketStatus.Cancelled);
        }
        finally { FakeVnPayService.Forget(txnRef); }
    }
}
