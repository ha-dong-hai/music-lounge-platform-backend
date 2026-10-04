using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Infrastructure.Persistence;
using MusicLounge.Tests.Integration.Helpers;

namespace MusicLounge.Tests.Integration.CF7;

/// <summary>
/// MLACP-616. Soat ve vao cua chuyen ve tu Confirmed sang Used. Ba bao cao doanh thu chi dem Confirmed, nen moi luot
/// soat ve lam doanh thu TUT di dung bang gia ve do — buoi dien cang dong, so cang thap. Do tren du lieu mau
/// 04/10/2026: trang thong ke cua chu phong tra chi hien khoang 1/3 doanh thu ve that (9,53 trieu so voi 28,11 trieu).
/// Doanh thu ghi nhan theo giao dich thuc te da thu tien (VAS 14), khong theo viec khach da vao cua hay chua.
///
/// Do bang CHENH LECH truoc/sau khi them dung mot ve da soat, vi du lieu seed dung chung giua cac lop test.
/// </summary>
[Collection("Integration")]
public sealed class RevenueCountsCheckedInTicketsTests
{
    private readonly ApiFactory _factory;

    public RevenueCountsCheckedInTicketsTests(ApiFactory factory) => _factory = factory;

    private static async Task<JsonElement> DataAsync(HttpClient c, string url)
    {
        var res = await c.GetAsync(url);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
    }

    private async Task<decimal> ThemMotVeDaSoatAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var gia = (await db.TicketPrices.AsNoTracking().SingleAsync(p => p.Id == SeedHelper.TicketPriceId)).Price;
        db.Tickets.Add(new Ticket
        {
            Id = Guid.NewGuid(), BuyerId = SeedHelper.AudienceId,
            PriceId = SeedHelper.TicketPriceId, TierId = SeedHelper.TicketTierId, ShowId = SeedHelper.ShowId,
            Status = TicketStatus.Used, PurchaseChannel = PurchaseChannel.Online,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
        return gia;
    }

    [Fact]
    public async Task VeDaSoat_VanLaDoanhThu_OCaBaBaoCao()
    {
        var owner = _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner");
        var admin = _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin");
        var urlThongKe = $"/api/v1/analytics/my-lounge?loungeId={SeedHelper.LoungeId}";
        var urlBaoCao = $"/api/v1/analytics/revenue-report?loungeId={SeedHelper.LoungeId}";

        var thongKeTruoc = (await DataAsync(owner, urlThongKe)).GetProperty("ticketRevenue").GetDecimal();
        var baoCaoTruoc = (await DataAsync(owner, urlBaoCao)).GetProperty("totalTicketRevenue").GetDecimal();
        var veBanTruoc = (await DataAsync(admin, "/api/v1/analytics/platform")).GetProperty("totalTicketsSold").GetInt32();

        var gia = await ThemMotVeDaSoatAsync();

        (await DataAsync(owner, urlThongKe)).GetProperty("ticketRevenue").GetDecimal()
            .Should().Be(thongKeTruoc + gia, "trang thống kê của chủ phòng trà phải tính vé đã soát");
        (await DataAsync(owner, urlBaoCao)).GetProperty("totalTicketRevenue").GetDecimal()
            .Should().Be(baoCaoTruoc + gia, "báo cáo doanh thu (bản xuất nộp kế toán) phải tính vé đã soát");
        (await DataAsync(admin, "/api/v1/analytics/platform")).GetProperty("totalTicketsSold").GetInt32()
            .Should().Be(veBanTruoc + 1, "vé đã soát là vé đã bán");
    }
}
