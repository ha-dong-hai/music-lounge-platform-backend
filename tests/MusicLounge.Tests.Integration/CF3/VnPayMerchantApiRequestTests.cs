using FluentAssertions;
using MusicLounge.Infrastructure.Services;

namespace MusicLounge.Tests.Integration.CF3;

/// <summary>
/// MLACP-614: hai dieu kien de mot lenh merchant API (refund / querydr) TOI DUOC VNPay. Ca hai deu tung thieu, nen
/// lenh hoan tien chua tung chay duoc tren sandbox: tuong lua tra 403 khi thieu User-Agent, va VNPay tra
/// "03 Invalid data format" khi vnp_OrderInfo co ky tu dac biet (do that ngay 04/10/2026).
/// Bo test nay KHONG goi VNPay — no chi giu hai dieu kien do khong bi go mat.
/// </summary>
[Collection("Integration")]
public sealed class VnPayMerchantApiRequestTests
{
    private readonly ApiFactory _factory;

    public VnPayMerchantApiRequestTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public void ClientVnPay_LuonGuiUserAgent()
    {
        var http = _factory.Services.GetRequiredService<IHttpClientFactory>().CreateClient("vnpay");

        http.DefaultRequestHeaders.UserAgent.ToString().Should().Be(VnPayService.UserAgent);
        VnPayService.UserAgent.Should().NotBeNullOrWhiteSpace();
    }

    [Theory]
    [InlineData("Hoan tien yeu cau #01a10521-0525-8892-8507-01a105210525", "Hoan tien yeu cau 01a10521-0525-8892-8507-01a105210525")]
    [InlineData("Doi soat giao dich #01a10521-0525-8892-8507-01a105210525", "Doi soat giao dich 01a10521-0525-8892-8507-01a105210525")]
    [InlineData("Hoan tien (lan 2) & doi soat!", "Hoan tien lan 2  doi soat")]
    public void OrderInfoGuiMerchantApi_KhongConKyTuDacBiet(string dauVao, string mongDoi)
    {
        var kq = VnPayService.MerchantApiOrderInfo(dauVao);

        kq.Should().Be(mongDoi);
        kq.Should().MatchRegex("^[A-Za-z0-9 -]*$");
    }
}
