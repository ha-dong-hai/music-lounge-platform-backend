using System.Net;
using FluentAssertions;

namespace MusicLounge.Tests.Integration.Payments;

/// <summary>
/// Hình dạng câu trả lời IPN mà VNPay đọc: khoá <c>RspCode</c> / <c>Message</c> viết hoa chữ đầu, đúng như tài
/// liệu VNPay và đúng như chính chú thích ở <c>PaymentsController</c> ("it reads RspCode").
///
/// <b>Vì sao cần test riêng:</b> ASP.NET mặc định đổi tên thuộc tính sang camelCase, nên record
/// <c>VnPayIpnResponse(RspCode, Message)</c> từng ra JSON <c>{"rspCode":..,"message":..}</c>. Mọi test IPN cũ đọc
/// bằng <c>ReadFromJsonAsync</c> — không phân biệt hoa thường — nên không test nào thấy được. Nếu phía VNPay so
/// khoá phân biệt hoa thường thì mọi IPN bị coi là merchant chưa xác nhận và bị gửi lại tới hết cửa sổ ~50 phút.
/// Test này đọc CHUỖI THÔ, trên cả bốn điểm nhận IPN (vé, ủng hộ, gọi món, gói dịch vụ).
/// </summary>
[Collection("Integration")]
public sealed class VnPayIpnResponseShapeTests
{
    private readonly ApiFactory _factory;

    public VnPayIpnResponseShapeTests(ApiFactory factory) => _factory = factory;

    [Theory]
    [InlineData("/api/v1/payments/vnpay/ipn")]
    [InlineData("/api/v1/donations/vnpay-ipn")]
    [InlineData("/api/v1/fnb-orders/vnpay-ipn")]
    [InlineData("/api/v1/subscriptions/vnpay-ipn")]
    public async Task IpnResponse_UsesVnPayKeyCasing(string path)
    {
        // Mã đơn không tồn tại: đủ để nhận một câu trả lời IPN thật mà không cần dựng dữ liệu —
        // thứ đang kiểm là HÌNH DẠNG câu trả lời, không phải nghiệp vụ.
        var res = await _factory.CreateClient()
            .GetAsync($"{path}?vnp_TxnRef=KHONG-TON-TAI&vnp_ResponseCode=00&vnp_Amount=1000000&vnp_TransactionNo=1");

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var raw = await res.Content.ReadAsStringAsync();
        raw.Should().Contain("\"RspCode\"").And.Contain("\"Message\"");
        raw.Should().NotContain("\"rspCode\"");
    }
}
