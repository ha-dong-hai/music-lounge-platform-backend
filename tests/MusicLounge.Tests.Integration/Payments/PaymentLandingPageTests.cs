using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using MusicLounge.Application.Common;
using MusicLounge.Application.Common.Settings;
using MusicLounge.Domain.Enums;
using MusicLounge.Infrastructure.Persistence;
using MusicLounge.Tests.Integration.Helpers;

namespace MusicLounge.Tests.Integration.Payments;

/// <summary>
/// MLACP-344. Khi VNPay báo thành công cho một bản ghi đã đóng
/// (<see cref="VnPayIpnOutcome.ConfirmedTooLate"/>), trình duyệt của khách từng bị đưa tới trang
/// <b>"thanh toán thất bại"</b>.
///
/// <para>Cả hai trang có sẵn đều nói sai. Trang thành công là nói dối — khách không có vé, không có
/// gì. Trang thất bại cũng sai — tiền <b>đã</b> rời khỏi tài khoản, và khách thấy "thất bại" có thể
/// mua lại lần nữa rồi bị trừ hai lần.</para>
/// </summary>
[Collection("Integration")]
public sealed class PaymentLandingPageTests
{
    private readonly ApiFactory _factory;

    public PaymentLandingPageTests(ApiFactory factory) => _factory = factory;

    private static BusinessSettings Settings(string processingUrl = "https://fe/payment/processing") => new()
    {
        PaymentSuccessUrl = "https://fe/payment/success",
        PaymentFailedUrl = "https://fe/payment/failed",
        PaymentProcessingUrl = processingUrl
    };

    // ── Quy tắc chọn trang ──────────────────────────────────────────────────

    [Fact]
    public void DaTraTienMaChuaDuocCapGiThiToiTrangDangXuLy()
    {
        VnPayIpnProtocol.BuyerLandingUrl(VnPayIpnOutcome.ConfirmedTooLate, Settings())
            .Should().Be("https://fe/payment/processing",
                "tiền đã rời tài khoản — nói 'thất bại' có thể khiến khách mua lại và bị trừ hai lần");
    }

    [Fact]
    public void ThanhCongVaThatBaiVanDiDungTrangCu()
    {
        VnPayIpnProtocol.BuyerLandingUrl(VnPayIpnOutcome.Confirmed, Settings())
            .Should().Be("https://fe/payment/success");
        VnPayIpnProtocol.BuyerLandingUrl(VnPayIpnOutcome.AlreadyProcessed, Settings())
            .Should().Be("https://fe/payment/success");
        VnPayIpnProtocol.BuyerLandingUrl(VnPayIpnOutcome.RecordedAsFailed, Settings())
            .Should().Be("https://fe/payment/failed");
        VnPayIpnProtocol.BuyerLandingUrl(VnPayIpnOutcome.AmountMismatch, Settings())
            .Should().Be("https://fe/payment/failed");
    }

    [Fact]
    public void ChuaCauHinhTrangDangXuLyThiQuayVeTrangThatBaiNhuCu()
    {
        // Không được làm vỡ môi trường đang chạy — production hiện chưa có đủ các khoá Business:*.
        VnPayIpnProtocol.BuyerLandingUrl(VnPayIpnOutcome.ConfirmedTooLate, Settings(processingUrl: ""))
            .Should().Be("https://fe/payment/failed");
    }

    // ── Đi qua HTTP thật ────────────────────────────────────────────────────

    private sealed record InitData(int DonationId, string OrderId, decimal Amount, string PaymentUrl);
    private sealed record InitResponse(bool Success, InitData Data);

    [Fact]
    public async Task TrinhDuyetQuayVeSauKhiDonDaDongThiToiTrangDangXuLy()
    {
        // Controller phải thật sự dùng quy tắc trên — không phải quy tắc đúng mà không ai gọi.
        var buyer = _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience");
        var created = await buyer.PostAsJsonAsync("/api/v1/donations", new
        {
            PerformanceId = SeedHelper.PerformanceId,
            Amount = 120_000m,
            IsAnonymous = false,
            IsMessagePublic = true
        });
        created.EnsureSuccessStatusCode();
        var init = (await created.Content.ReadFromJsonAsync<InitResponse>())!.Data;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            (await db.Donations.FindAsync(init.DonationId))!.Status = DonationStatus.Cancelled;
            await db.SaveChangesAsync();
        }

        var browser = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var res = await browser.GetAsync(
            $"/api/v1/donations/vnpay-return?vnp_TxnRef={Uri.EscapeDataString(init.OrderId)}" +
            "&vnp_ResponseCode=00&vnp_Amount=12000000");

        res.StatusCode.Should().Be(HttpStatusCode.Redirect);
        res.Headers.Location!.ToString().Should().Be("http://localhost/payment/processing");
    }
}
