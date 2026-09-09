using System.Net;
using System.Net.Http.Json;
using System.Web;
using FluentAssertions;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MusicLounge.Application.Common;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Donations.Commands.CreateDonation;
using MusicLounge.Application.FnbMenuItems.Commands.CreateMenuItem;
using MusicLounge.Application.FnbMenuItems.Commands.UpdateMenuItem;
using MusicLounge.Application.Refunds.Commands.ProcessRefundRequest;
using MusicLounge.Application.Subscriptions.Commands.CreateSubscriptionPackage;
using MusicLounge.Application.Subscriptions.Commands.UpdateSubscriptionPackage;
using MusicLounge.Application.TicketTiers.Commands.CreateTicketTier;
using MusicLounge.Infrastructure.Services;
using MusicLounge.Infrastructure.Settings;
using MusicLounge.Tests.Integration.Helpers;

namespace MusicLounge.Tests.Integration.Payments;

/// <summary>
/// MLACP-332. Đồng Việt Nam không có đơn vị nhỏ hơn 1đ, nhưng không validator tiền nào nói điều đó
/// — mọi cột tiền là <c>decimal(x,2)</c> và mọi rule chỉ nói "lớn hơn 0".
///
/// <para><b>Vì sao đó là mất tiền thật.</b> Hai tầng xử lý số lẻ ngược chiều nhau: EF Core
/// <b>làm tròn</b> khi ghi xuống database (<c>10000.999</c> thành <c>10001.00</c>), còn
/// <c>VnPayService</c> <b>cắt cụt</b> khi gửi sang VNPay (thu <c>10000.99</c>). Khách trả theo số
/// VNPay thu; callback quay về gặp chốt chống giả mạo trong
/// <c>ProcessDonationPaymentCommandHandler</c>, thấy hai số không khớp nên từ chối. Donation nằm
/// mãi ở <c>PendingPayment</c>: <b>khách mất tiền, hệ thống không ghi nhận gì.</b></para>
///
/// <para><b>Giới hạn của chính bộ kiểm thử này — đọc kỹ trước khi tin.</b> Bộ test chạy trên SQLite,
/// và SQLite <b>không cưỡng chế độ chính xác thập phân</b>: nó lưu nguyên <c>10000.999</c> thay vì
/// làm tròn về <c>10001.00</c> như SQL Server. Nên <b>không bài nào ở đây tái hiện được cảnh
/// donation bị treo</b> — chuyện đó được chứng minh bằng cách chạy riêng EF Core 8.0.11 +
/// Microsoft.Data.SqlClient 5.2.3 trên SQL Server thật, kết quả:
/// <c>10000.005</c> vào thì database giữ <c>10000.01</c> còn VNPay được yêu cầu thu
/// <c>10000.00</c>; <c>10000.999</c> vào thì database giữ <c>10001.00</c> còn VNPay thu
/// <c>10000.99</c>. Bất kỳ số nào có chữ số thập phân thứ ba từ 5 trở lên đều lệch.</para>
///
/// <para>Những gì các bài dưới đây thực sự chứng minh, không hơn: (1) mọi cửa vào tiền do client
/// nhập đều từ chối số lẻ, và (2) <c>VnPayService</c> đã làm tròn thay vì cắt cụt. Cả hai đều đỏ khi
/// tắt bản vá.</para>
/// </summary>
[Collection("Integration")]
public sealed class WholeDongMoneyTests
{
    private readonly ApiFactory _factory;

    public WholeDongMoneyTests(ApiFactory factory) => _factory = factory;

    /// <summary>
    /// Lấy validator qua DI thay vì <c>new</c> trực tiếp: phần lớn validator là <c>internal</c> và
    /// chỉ Infrastructure mở internals cho test project, còn <c>IValidator&lt;T&gt;</c> thì luôn
    /// public. Cách này cũng cấp luôn <c>IUnitOfWork</c> cho những validator có rule tra database.
    /// </summary>
    private async Task<IReadOnlyList<string>> ErrorsAsync<TCommand>(TCommand command)
    {
        using var scope = _factory.Services.CreateScope();
        var validator = scope.ServiceProvider.GetRequiredService<IValidator<TCommand>>();
        var result = await validator.ValidateAsync(command!);
        return result.Errors.Select(e => e.ErrorMessage).ToList();
    }

    // Cửa vào nghiêm trọng: donate.

    [Fact]
    public async Task DonateSoLeBiTuChoiNgayTuCuaVao()
    {
        // Số này có chữ số thập phân thứ ba là 9 — đúng loại số làm hai tầng tách nhau.
        var errors = await ErrorsAsync(new CreateDonationCommand(
            SeedHelper.PerformanceId, 10_000.999m, false, null, false, "127.0.0.1"));

        errors.Should().Contain(MoneyAmount.NotWholeDongMessage);
    }

    [Fact]
    public async Task DonateSoNguyenDongVanQua()
    {
        // Chiều ngược lại: chốt mới không được chặn nhầm giao dịch bình thường.
        var errors = await ErrorsAsync(new CreateDonationCommand(
            SeedHelper.PerformanceId, 200_000m, false, null, false, "127.0.0.1"));

        errors.Should().NotContain(MoneyAmount.NotWholeDongMessage);
    }

    [Fact]
    public async Task DonateSoLeBiApiTraVe400()
    {
        // Bài trên gọi thẳng validator; bài này đi qua HTTP thật để chắc rằng pipeline có chạy
        // validator đó chứ không phải nó được đăng ký rồi bỏ quên.
        var client = _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience");

        var res = await client.PostAsJsonAsync("/api/v1/donations", new
        {
            PerformanceId = SeedHelper.PerformanceId,
            Amount = 10_000.999m,
            IsAnonymous = false,
            IsMessagePublic = true
        });

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await res.Content.ReadAsStringAsync()).Should().Contain("số nguyên đồng");
    }

    // Cửa vào âm thầm: số tiền Admin duyệt hoàn.

    [Fact]
    public async Task SoTienDuyetHoanSoLeBiTuChoi()
    {
        // Khác donate, đường hoàn tiền KHÔNG có chốt đối chiếu số tiền nào — số lẻ không bị chặn
        // lại mà ghi vào sổ cái một con số khác với số VNPay thật sự hoàn.
        var errors = await ErrorsAsync(new ProcessRefundRequestCommand(
            1, "Approved", 50_000.999m, "127.0.0.1"));

        errors.Should().Contain(MoneyAmount.NotWholeDongMessage);
    }

    [Fact]
    public async Task DuyetHoanBoTrongSoTienVanQua()
    {
        // Bỏ trống nghĩa là "hoàn đúng số đã yêu cầu" — chốt này chỉ nói về hình dạng của số khi đã
        // có số, không được biến việc bỏ trống thành lỗi.
        var errors = await ErrorsAsync(new ProcessRefundRequestCommand(
            1, "Approved", null, "127.0.0.1"));

        errors.Should().NotContain(MoneyAmount.NotWholeDongMessage);
    }

    // Các cửa vào giá.

    [Fact]
    public async Task MoiCuaVaoGiaDeuChanSoLe()
    {
        // Năm cửa còn lại không gây lệch với VNPay (tiền được đọc lại từ database trước khi thanh
        // toán) nhưng vẫn lưu được những mức giá không ai trả được. Chặn ở nguồn.
        await Check(new CreateMenuItemCommand(1, "Food", "Mon", null, 55_000.5m, null, 0), "tạo món");
        await Check(new UpdateMenuItemCommand(1, "Food", "Mon", null, 55_000.5m, null, true, 0), "sửa món");
        await Check(new CreateSubscriptionPackageCommand("Goi", null, 199_000.5m, "Monthly", 10, false, 0, 0), "tạo gói");
        await Check(new UpdateSubscriptionPackageCommand(1, null, 199_000.5m, 10, false, 0, 0, true), "sửa gói");
        await Check(new CreateTicketTierCommand(
            SeedHelper.ShowId, "Hang thuong", null, "Livestream", null, 100,
            new[]
            {
                new TicketPriceInput("Dot 1", 150_000.5m, 50, "Online",
                    DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1))
            }), "tạo hạng vé");

        async Task Check<TCommand>(TCommand command, string ten)
            => (await ErrorsAsync(command)).Should().Contain(MoneyAmount.NotWholeDongMessage,
                "cửa vào {0} phải chặn giá lẻ", ten);
    }

    // Lớp phòng thủ thứ hai: mã hoá số tiền gửi VNPay.

    private static VnPayService NewVnPayService() => new(
        Options.Create(new VnPaySettings
        {
            TmnCode = "TESTCODE",
            HashSecret = "TEST-HASH-SECRET-NOT-REAL-1234567890",
            PaymentUrl = "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html",
            Version = "2.1.0"
        }),
        new StubHttpClientFactory(),
        NullLogger<VnPayService>.Instance);

    private static string? AmountSentTo(string url)
        => HttpUtility.ParseQueryString(new Uri(url).Query)["vnp_Amount"];

    [Fact]
    public void VnPayNhanSoTienLamTronChuKhongCatCut()
    {
        // Spec VNPay: vnp_Amount là SỐ NGUYÊN bằng số tiền nhân 100, và cách làm được tài liệu hoá
        // là Math.round(amount * 100). Code cũ ép kiểu (long) — cắt cụt. Với 10000.999 thì cắt cụt
        // ra 1000099 (thu 10000.99), làm tròn ra 1000100 (thu 10001.00) — khớp cách EF Core ghi
        // xuống database, nên hai tầng không còn tách nhau.
        //
        // Validator ở trên đã chặn số lẻ rồi, nên đường này về nguyên tắc không còn nhận số lẻ nữa.
        // Bài kiểm tra vẫn giữ vì đây là lớp phòng thủ thứ hai: một cửa vào bị bỏ sót trong tương
        // lai sẽ không lặp lại đúng lỗi mất tiền này.
        var url = NewVnPayService().CreatePaymentUrl(new VnPayPaymentRequest(
            OrderId: "DON-TEST-1",
            Amount: 10_000.999m,
            OrderInfo: "Test",
            ReturnUrl: "https://example.test/return",
            IpAddress: "127.0.0.1"));

        AmountSentTo(url).Should().Be("1000100",
            "cắt cụt sẽ ra 1000099 — tức thu ít hơn số đã ghi sổ");
    }

    [Fact]
    public void VnPaySoNguyenDongKhongDoiGiaTri()
    {
        // Chiều ngược lại: đổi cắt cụt thành làm tròn không được làm lệch những số vốn đã đúng.
        var url = NewVnPayService().CreatePaymentUrl(new VnPayPaymentRequest(
            OrderId: "DON-TEST-2",
            Amount: 200_000m,
            OrderInfo: "Test",
            ReturnUrl: "https://example.test/return",
            IpAddress: "127.0.0.1"));

        AmountSentTo(url).Should().Be("20000000");
    }

    /// <summary>CreatePaymentUrl không gọi HTTP; stub này chỉ để dựng được service.</summary>
    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new();
    }
}
