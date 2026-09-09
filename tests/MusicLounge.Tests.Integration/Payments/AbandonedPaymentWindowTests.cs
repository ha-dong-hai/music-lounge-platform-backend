using FluentAssertions;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Infrastructure.Jobs;
using MusicLounge.Infrastructure.Persistence;
using MusicLounge.Tests.Integration.Helpers;

namespace MusicLounge.Tests.Integration.Payments;

/// <summary>
/// MLACP-333. <c>CancelAbandonedPaymentsJob</c> tuyên bố một thanh toán là "bỏ rơi" rồi huỷ vé và
/// đánh <c>Failed</c>. Ngưỡng cũ là 30 phút, dựa trên một chú thích ghi *"VNPay retries the callback
/// for ~15 minutes"*.
///
/// <para><b>Con số 15 phút đó sai.</b> Tài liệu chính chủ VNPay (sandbox.vnpayment.vn, mục IPN URL)
/// ghi rõ: IPN được gọi lại <b>tối đa 10 lần, mỗi lần cách nhau 5 phút</b> — lần cuối có thể rơi vào
/// khoảng phút thứ 50. Nên biên an toàn mà chú thích cũ tưởng là +15 phút thực ra là <b>âm 20
/// phút</b>: từ phút 30 đến phút 50, VNPay vẫn đang retry hợp lệ trong khi vé đã bị huỷ.</para>
///
/// <para><b>Hậu quả.</b> Khách trả tiền thật; endpoint IPN không nhận được (deploy + restart, lỗi
/// mạng thoáng qua); VNPay retry; đến phút 35 nó gọi lại được thì vé đã bị huỷ, thanh toán đã
/// <c>Failed</c>. Handler thấy <c>Status != Pending</c> nên coi là callback trùng lặp, ghi log mức
/// <c>Information</c> và bỏ đi. Khách mất tiền, không có vé, thấy trang thất bại — và không ai tìm
/// ra được vì <b>không một chỗ nào trong toàn bộ src/ đọc <c>PaymentStatus.Failed</c></b>.</para>
///
/// <para>Bài kiểm tra ở đây bắt đúng khoảng trống đó: một thanh toán 35 phút tuổi phải còn nguyên.
/// Việc xử lý callback đến muộn và việc trả sai mã RspCode cho VNPay là task riêng.</para>
/// </summary>
[Collection("Integration")]
public sealed class AbandonedPaymentWindowTests
{
    /// <summary>
    /// Cửa sổ retry IPN theo tài liệu VNPay: 10 lần, mỗi lần cách 5 phút. Đây là con số ngưỡng huỷ
    /// phải vượt qua, không phải một hằng số tuỳ chọn.
    /// </summary>
    private const int VnPayIpnRetryWindowMinutes = 10 * 5;

    private readonly ApiFactory _factory;

    public AbandonedPaymentWindowTests(ApiFactory factory) => _factory = factory;

    private async Task<(int PaymentId, Guid TicketId)> PendingPurchaseAsync(int ageMinutes)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var payment = new Payment
        {
            OrderId = $"MLACP333-{Guid.NewGuid():N}"[..30],
            GrossAmount = 100_000m,
            Status = PaymentStatus.Pending,
            ReferenceType = "TicketHold",
            ReferenceId = "0",
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-ageMinutes)
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
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.Add(ticket);
        await db.SaveChangesAsync();

        return (payment.Id, ticket.Id);
    }

    private async Task RunJobAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var job = scope.ServiceProvider.GetRequiredService<CancelAbandonedPaymentsJob>();
        await job.ExecuteAsync(new JobCancellationToken(false));
    }

    [Fact]
    public async Task ThanhToanConTrongCuaSoRetryCuaVnPayThiKhongDuocHuy()
    {
        // 35 phút: đã quá ngưỡng cũ 30 phút, nhưng VNPay vẫn còn tới ~15 phút retry nữa. Đây đúng
        // là chiếc vé mà ngưỡng cũ huỷ oan.
        var (paymentId, ticketId) = await PendingPurchaseAsync(ageMinutes: 35);

        await RunJobAsync();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        (await db.Payments.FindAsync(paymentId))!.Status
            .Should().Be(PaymentStatus.Pending,
                "VNPay còn đang retry — đánh Failed lúc này là vứt đi một xác nhận sắp tới");
        (await db.Tickets.FindAsync(ticketId))!.Status
            .Should().Be(TicketStatus.Pending,
                "huỷ vé của người có thể vừa trả tiền xong là tác hại nặng hơn nhiều so với việc " +
                "giữ chỗ thêm vài chục phút");
    }

    [Fact]
    public async Task ThanhToanQuaHanCuaSoRetryThiVanPhaiDuocHuy()
    {
        // Chiều ngược lại: nới ngưỡng không được biến job thành vô dụng, nếu không giỏ hàng bỏ dở
        // sẽ giữ chỗ vĩnh viễn và người mua thật không còn vé để mua.
        var (paymentId, ticketId) = await PendingPurchaseAsync(ageMinutes: 120);

        await RunJobAsync();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        (await db.Payments.FindAsync(paymentId))!.Status.Should().Be(PaymentStatus.Failed);
        (await db.Tickets.FindAsync(ticketId))!.Status.Should().Be(TicketStatus.Cancelled);
    }

    [Fact]
    public void NguongHuyPhaiLonHonCuaSoRetryCuaVnPay()
    {
        // Chốt bất biến, không phải chốt giá trị: nếu sau này ai đó chỉnh ngưỡng xuống cho "thoáng
        // chỗ nhanh hơn", bài này đỏ và nói rõ vì sao không được làm thế.
        CancelAbandonedPaymentsJob.DefaultAbandonMinutes
            .Should().BeGreaterThan(VnPayIpnRetryWindowMinutes,
                "VNPay gọi lại IPN tối đa 10 lần cách nhau 5 phút; huỷ vé trước khi cửa sổ đó đóng " +
                "là vứt bỏ một xác nhận thanh toán thật");
    }
}
