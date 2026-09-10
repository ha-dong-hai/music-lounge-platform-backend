using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MusicLounge.Application.Common;
using MusicLounge.Domain.Enums;
using MusicLounge.Infrastructure.Persistence;
using MusicLounge.Tests.Integration.Helpers;

namespace MusicLounge.Tests.Integration.Payments;

/// <summary>
/// MLACP-334. Cả bốn luồng thanh toán đều có cùng một đoạn:
///
/// <code>
/// if (payment.Status != PaymentStatus.Pending)
///     return payment.Status == PaymentStatus.Confirmed;
/// </code>
///
/// <para>Đoạn này gộp hai tình huống hoàn toàn khác nhau vào một kết quả: <b>callback trùng lặp của
/// một bản ghi đã xong</b> (vô hại), và <b>VNPay báo THÀNH CÔNG cho một bản ghi đã bị đóng</b> —
/// tức tiền thật đã thu mà hệ thống không còn ghi nhận. Cái thứ hai biến mất sau một dòng log mức
/// <c>Information</c>; riêng đường F&amp;B thì không ghi gì cả.</para>
///
/// <para>Vì <b>không một chỗ nào trong toàn bộ <c>src/</c> đọc <c>PaymentStatus.Failed</c></b>, đó là
/// tiền mất không dấu vết.</para>
///
/// <para><b>Cố ý không tự khôi phục.</b> Cấp lại vé ngay trong callback có thể làm vượt sức chứa
/// thật của phòng trà, còn quyết định hoàn tiền thuộc chính sách chứ không thuộc một handler. Việc
/// đúng ở tầng này là đảm bảo sự cố không biến mất — nên bài kiểm tra dưới đây khẳng định bản ghi
/// <b>vẫn giữ nguyên</b> trạng thái đóng, và cái thay đổi là <b>có người được báo</b>.</para>
/// </summary>
[Collection("Integration")]
public sealed class LateVnPayConfirmationTests
{
    private readonly ApiFactory _factory;

    public LateVnPayConfirmationTests(ApiFactory factory) => _factory = factory;

    private sealed record InitData(int DonationId, string OrderId, decimal Amount, string PaymentUrl);
    private sealed record InitResponse(bool Success, InitData Data);
    private sealed record IpnBody(string RspCode, string Message);

    private async Task<(int DonationId, string OrderId)> NewDonationAsync(decimal amount)
    {
        var client = _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience");
        var res = await client.PostAsJsonAsync("/api/v1/donations", new
        {
            PerformanceId = SeedHelper.PerformanceId,
            Amount = amount,
            IsAnonymous = false,
            IsMessagePublic = true
        });
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<InitResponse>();
        return (body!.Data.DonationId, body.Data.OrderId);
    }

    private async Task<IpnBody> CallIpnAsync(string orderId, decimal amount, bool success = true)
    {
        var client = _factory.CreateClient();
        var qs = $"?vnp_TxnRef={Uri.EscapeDataString(orderId)}" +
                 $"&vnp_ResponseCode={(success ? "00" : "24")}" +
                 $"&vnp_Amount={(long)(amount * 100)}";
        var res = await client.GetAsync($"/api/v1/donations/vnpay-ipn{qs}");
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<IpnBody>())!;
    }

    private async Task<int> IncidentCountAsync(int donationId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        // Loc theo dung mot Admin cua seed: xu ly su co bao cho MOI Admin, nen dem tong se thay
        // doi theo so tai khoan Admin dang ton tai — bai kiem tra khong duoc phu thuoc vao con so do.
        return await db.Notifications.CountAsync(
            n => n.Type == NotificationType.PaymentConfirmedAfterExpiry
                 && n.UserId == SeedHelper.AdminId
                 && n.ReferenceType == "donation"
                 && n.ReferenceId == donationId.ToString());
    }

    [Fact]
    public async Task VnPayXacNhanThanhCongSauKhiDonDaDongThiPhaiThanhSuCoCoNguoiBiet()
    {
        var (donationId, orderId) = await NewDonationAsync(200_000m);

        // ExpireStuckDonationsJob đã đóng bản ghi này — nhưng khách thì đã trả tiền rồi.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var donation = await db.Donations.FindAsync(donationId);
            donation!.Status = DonationStatus.Cancelled;
            await db.SaveChangesAsync();
        }

        (await IncidentCountAsync(donationId)).Should().Be(0, "chưa có gì xảy ra");

        var ipn = await CallIpnAsync(orderId, 200_000m);

        ipn.RspCode.Should().Be("02",
            "trả 99 thì VNPay coi là lỗi tạm thời và gọi lại đủ 10 lần vào đúng ngõ cụt đó");

        (await IncidentCountAsync(donationId)).Should().Be(1,
            "đây là tiền thật đã thu mà hệ thống không cấp được gì — phải có người biết, " +
            "không phải một dòng log Information");

        using var verify = _factory.Services.CreateScope();
        var verifyDb = verify.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await verifyDb.Donations.FindAsync(donationId))!.Status
            .Should().Be(DonationStatus.Cancelled,
                "cố ý không tự khôi phục: quyết định cấp lại hay hoàn tiền là của người, " +
                "không phải của handler callback");
    }

    [Fact]
    public async Task CallbackTrungLapBinhThuongTraVe02ChuKhongPhai99()
    {
        var (donationId, orderId) = await NewDonationAsync(150_000m);

        var first = await CallIpnAsync(orderId, 150_000m);
        first.RspCode.Should().Be("00");

        var replay = await CallIpnAsync(orderId, 150_000m);

        replay.RspCode.Should().Be("02",
            "đây là callback trùng lặp hoàn toàn bình thường — bắt VNPay gọi lại 10 lần cho nó " +
            "là tự gây tải và làm bảng điều khiển VNPay hiện giao dịch như chưa xác nhận");

        (await IncidentCountAsync(donationId)).Should().Be(0,
            "trùng lặp trên một bản ghi đã Confirmed thì vô hại, không được báo động giả");
    }

    [Fact]
    public async Task LechSoTienTraVe04ChuKhongPhai99()
    {
        var (_, orderId) = await NewDonationAsync(100_000m);

        var ipn = await CallIpnAsync(orderId, 999_000m);

        ipn.RspCode.Should().Be("04", "VNPay có mã riêng cho lệch số tiền");
    }

    [Fact]
    public async Task KhongTimThayDonTraVe01ChuKhongPhai99()
    {
        var ipn = await CallIpnAsync($"KHONG-TON-TAI-{Guid.NewGuid():N}"[..30], 100_000m);

        ipn.RspCode.Should().Be("01");
    }

    [Fact]
    public void BangMaTraVeChoVnPayPhaiDungTungTruongHop()
    {
        // Chốt cả bảng, vì mỗi ô sai là một kiểu hỏng khác nhau: 00/02 dừng retry, còn
        // 01/04/97/99 khiến VNPay gọi lại tối đa 10 lần, mỗi lần cách 5 phút.
        VnPayIpnProtocol.ResponseFor(VnPayIpnOutcome.Confirmed).RspCode.Should().Be("00");
        VnPayIpnProtocol.ResponseFor(VnPayIpnOutcome.RecordedAsFailed).RspCode.Should().Be("00");
        VnPayIpnProtocol.ResponseFor(VnPayIpnOutcome.AlreadyProcessed).RspCode.Should().Be("02");
        VnPayIpnProtocol.ResponseFor(VnPayIpnOutcome.ConfirmedTooLate).RspCode.Should().Be("02");
        VnPayIpnProtocol.ResponseFor(VnPayIpnOutcome.OrderNotFound).RspCode.Should().Be("01");
        VnPayIpnProtocol.ResponseFor(VnPayIpnOutcome.AmountMismatch).RspCode.Should().Be("04");
        VnPayIpnProtocol.ResponseFor(VnPayIpnOutcome.InvalidSignature).RspCode.Should().Be("97");
        VnPayIpnProtocol.ResponseFor(VnPayIpnOutcome.InternalError).RspCode.Should().Be("99");
    }

    [Fact]
    public void KhachKhongDuocBaoThanhCongKhiHoKhongCoGiCa()
    {
        // ConfirmedTooLate nghĩa là đã thu tiền nhưng không cấp được gì. Cho khách xem trang thành
        // công là nói dối họ. Trang đúng phải là trang thứ ba, hiện chưa có — cần frontend.
        VnPayIpnProtocol.IsBuyerFacingSuccess(VnPayIpnOutcome.ConfirmedTooLate)
            .Should().BeFalse();
        VnPayIpnProtocol.IsBuyerFacingSuccess(VnPayIpnOutcome.Confirmed).Should().BeTrue();
        VnPayIpnProtocol.IsBuyerFacingSuccess(VnPayIpnOutcome.AlreadyProcessed).Should().BeTrue();
    }
}
