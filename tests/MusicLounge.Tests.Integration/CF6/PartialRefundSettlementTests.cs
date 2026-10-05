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

namespace MusicLounge.Tests.Integration.CF6;

/// <summary>
/// MLACP-615. Mot thanh toan co the mua nhieu ve, va moi ve duoc huy / hoan rieng — nen mot thanh toan co the bi hoan
/// nhieu lan. Ca hai phep doi chieu ke toan duoi day phai dung sau BAT KY chuoi hoan nao cong lai du 100%:
///   1. Phan chua tra cho phong tra (cac dot quyet toan chua giai ngan) ve dung 0 — khong con dong nao cho tra.
///   2. Tong da dao cua tung khoan (phi nen tang, thue GTGT, phan giu ho chu phong tra, tien ra khoi cong) bang dung
///      so da ghi luc mua.
/// Va moi so tien ghi ra phai la dong nguyen (VND khong co don vi le — Luat Ke toan 2015 Dieu 11 lay Dong Viet Nam lam
/// don vi tinh; khong ngan hang hay cong thanh toan nao chuyen duoc 0,21 dong).
///
/// Truoc khi sua: moi lan hoan nhan ti le (lan nay / tong goc) vao phan CON LAI sau cac lan truoc, nen lan hoan sau bi
/// giam it hon phan phai giam; va moi phep tinh lam tron toi 2 chu so le.
/// </summary>
[Collection("Integration")]
public sealed class PartialRefundSettlementTests
{
    private readonly ApiFactory _factory;

    public PartialRefundSettlementTests(ApiFactory factory) => _factory = factory;

    private sealed record Seeded(Guid PaymentId, IReadOnlyList<Guid> RefundIds);

    private async Task<Seeded> SeedAsync(decimal gross, decimal fee, decimal tax, decimal partial, decimal[] refundAmounts)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var show = new LoungeShow
        {
            LoungeId = SeedHelper.LoungeId,
            Name = $"HoanNhieuLan-{Guid.NewGuid():N}",
            Description = "Buoi dien sap toi, khach huy tung ve",
            Format = LoungeShowFormat.Offline,
            Status = LoungeShowStatus.Published,
            ScheduledStart = DateTimeOffset.UtcNow.AddDays(10),
            ScheduledEnd = DateTimeOffset.UtcNow.AddDays(10).AddHours(3)
        };
        db.LoungeShows.Add(show);
        await db.SaveChangesAsync();

        var net = gross - fee - tax;
        var payment = new Payment
        {
            OrderId = $"PRS-{Guid.NewGuid():N}"[..30],
            GrossAmount = gross, PlatformFee = fee, TaxWithheld = tax, NetAmount = net,
            Method = PaymentMethod.Gateway, Status = PaymentStatus.Confirmed,
            TransactionId = $"PRS{Guid.NewGuid():N}"[..16],
            ReferenceType = "TicketHold", ReferenceId = "0",
            PaidAt = DateTimeOffset.UtcNow.AddDays(-1), CreatedAt = DateTimeOffset.UtcNow.AddDays(-1)
        };
        db.Payments.Add(payment);
        await db.SaveChangesAsync();

        foreach (var _ in refundAmounts)
            db.Tickets.Add(new Ticket
            {
                Id = Guid.NewGuid(), BuyerId = SeedHelper.AudienceId,
                PriceId = SeedHelper.TicketPriceId, TierId = SeedHelper.TicketTierId,
                ShowId = show.Id, PaymentId = payment.Id, Status = TicketStatus.Cancelled,
                PurchaseChannel = PurchaseChannel.Online, CreatedAt = DateTimeOffset.UtcNow.AddDays(-1)
            });

        db.Settlements.Add(new Settlement
        {
            OwnerId = SeedHelper.OwnerId, PaymentId = payment.Id, ReleaseType = SettlementReleaseType.Partial70,
            GrossAmount = gross, PreRateApplied = 0.70m, PostRateApplied = 0.30m,
            NetAmount = partial, Status = SettlementStatus.Scheduled,
            ScheduledAt = DateTimeOffset.UtcNow.AddDays(12), CreatedAt = DateTimeOffset.UtcNow.AddDays(-1)
        });
        db.Settlements.Add(new Settlement
        {
            OwnerId = SeedHelper.OwnerId, PaymentId = payment.Id, ReleaseType = SettlementReleaseType.Final30,
            GrossAmount = gross, PreRateApplied = 0.70m, PostRateApplied = 0.30m,
            NetAmount = net - partial, Status = SettlementStatus.Scheduled,
            ScheduledAt = DateTimeOffset.UtcNow.AddDays(24), CreatedAt = DateTimeOffset.UtcNow.AddDays(-1)
        });

        var ids = new List<Guid>();
        foreach (var amount in refundAmounts)
        {
            var r = new RefundRequest
            {
                PaymentId = payment.Id, RequestedBy = SeedHelper.AudienceId, Reason = "Khách huỷ một vé",
                AmountRequested = amount, RefundPercentage = 100m, Status = RefundRequestStatus.Pending
            };
            db.RefundRequests.Add(r);
            ids.Add(r.Id);
        }
        await db.SaveChangesAsync();
        return new Seeded(payment.Id, ids);
    }

    private async Task<(List<Settlement> Dot, List<LedgerEntry> But)> DocAsync(Guid paymentId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var dot = await db.Settlements.AsNoTracking().Where(s => s.PaymentId == paymentId).ToListAsync();
        var but = await db.LedgerEntries.AsNoTracking().Include(e => e.Account)
            .Where(e => e.PaymentId == paymentId && e.ReferenceType == LedgerReferenceTypes.Refund).ToListAsync();
        return (dot, but);
    }

    [Theory]
    // Mua 2 ve, huy ca 2.
    [InlineData(200_000, 10_000, 10_000, 126_000, new[] { 100_000, 100_000 })]
    // Ba lan hoan khong chia het: moi ti le nhan ra so le neu khong lam tron ve dong.
    [InlineData(100_000, 5_000, 5_000, 63_000, new[] { 33_333, 33_333, 33_334 })]
    public async Task HoanDuTungPhan_PhongTraKhongConDongNaoChoTra_VaMoiKhoanDaoDungBangLucMua(
        int gross, int fee, int tax, int partial, int[] lanHoan)
    {
        var s = await SeedAsync(gross, fee, tax, partial, lanHoan.Select(x => (decimal)x).ToArray());
        var admin = _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin");

        foreach (var id in s.RefundIds)
        {
            var res = await admin.PostAsJsonAsync($"/api/v1/admin/refund-requests/{id}/process",
                new { Decision = "Approved", ApprovedAmount = (decimal?)null });
            res.StatusCode.Should().Be(HttpStatusCode.NoContent);

            var (dotGiua, butGiua) = await DocAsync(s.PaymentId);
            dotGiua.Should().OnlyContain(d => d.NetAmount == decimal.Truncate(d.NetAmount),
                "đợt quyết toán phải là đồng nguyên sau mỗi lần hoàn");
            butGiua.Should().OnlyContain(e => e.Amount == decimal.Truncate(e.Amount),
                "bút toán hoàn phải là đồng nguyên");
        }

        var (dot, but) = await DocAsync(s.PaymentId);

        dot.Sum(d => d.NetAmount).Should().Be(0m, "đã hoàn đủ 100% cho khách thì không còn đồng nào để trả phòng trà");
        dot.Should().OnlyContain(d => d.Status == SettlementStatus.Cancelled,
            "đợt về 0đ phải được huỷ, không để job đi 'giải ngân 0đ' và báo chủ phòng trà là đã nhận tiền");

        but.Where(e => !e.IsDebit && e.Account.OwnerType == AccountType.Gateway).Sum(e => e.Amount)
            .Should().Be(gross, "tổng tiền ra khỏi cổng bằng đúng số khách đã trả");
        but.Where(e => e.IsDebit && e.Account.OwnerType == AccountType.Tax).Sum(e => e.Amount)
            .Should().Be(tax, "thuế GTGT đã khấu trừ phải được trả lại đủ, không thừa không thiếu");
        but.Where(e => e.IsDebit && e.Account.OwnerType == AccountType.Platform).Sum(e => e.Amount)
            .Should().Be(gross - tax, "nền tảng trả lại đủ phí của mình cộng phần đang giữ hộ phòng trà");
    }

    [Fact]
    public async Task HoanMotPhan_DotQuyetToanGiamDungPhanTuongUng_VanConChoTra()
    {
        var s = await SeedAsync(200_000, 10_000, 10_000, 126_000, new[] { 100_000m, 100_000m });
        var admin = _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin");

        (await admin.PostAsJsonAsync($"/api/v1/admin/refund-requests/{s.RefundIds[0]}/process",
            new { Decision = "Approved", ApprovedAmount = (decimal?)null })).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var (dot, _) = await DocAsync(s.PaymentId);
        dot.Single(d => d.ReleaseType == SettlementReleaseType.Partial70).NetAmount.Should().Be(63_000m);
        dot.Single(d => d.ReleaseType == SettlementReleaseType.Final30).NetAmount.Should().Be(27_000m);
        dot.Should().OnlyContain(d => d.Status == SettlementStatus.Scheduled, "vẫn còn một vé chưa hoàn, phòng trà vẫn được nhận phần đó");
    }

    [Fact]
    public async Task HoanLanThuHaiTrongBa_PhongTraConDungPhanCuaMotVe()
    {
        // Mua 3 ve 300.000d (phong tra nhan 270.000d: 189.000 + 81.000), hoan 2 ve.
        var s = await SeedAsync(300_000, 15_000, 15_000, 189_000, new[] { 100_000m, 100_000m, 100_000m });
        var admin = _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin");
        foreach (var id in s.RefundIds.Take(2))
            (await admin.PostAsJsonAsync($"/api/v1/admin/refund-requests/{id}/process",
                new { Decision = "Approved", ApprovedAmount = (decimal?)null })).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var (dot, _) = await DocAsync(s.PaymentId);
        dot.Single(d => d.ReleaseType == SettlementReleaseType.Partial70).NetAmount.Should().Be(63_000m,
            "còn 1 vé trong 3 thì phòng trà còn đúng 1/3 của 189.000đ");
        dot.Single(d => d.ReleaseType == SettlementReleaseType.Final30).NetAmount.Should().Be(27_000m);
    }

    [Fact]
    public async Task PhiLucMuaBiLeXu_HoanDuThiMoiKhoanVanDongVeDungSoLucMua()
    {
        // PaymentFeeCalculator lam tron phi/thue toi 2 chu so: ve 1.111d -> phi 5% = 55,55d. Lan hoan cuoi phai nhan
        // dung phan con lai, neu khong tai khoan nen tang va thue con treo vai xu mai mai.
        var s = await SeedAsync(1_111m, 55.55m, 55.55m, 699m, new[] { 555m, 556m });
        var admin = _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin");
        foreach (var id in s.RefundIds)
            (await admin.PostAsJsonAsync($"/api/v1/admin/refund-requests/{id}/process",
                new { Decision = "Approved", ApprovedAmount = (decimal?)null })).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var (dot, but) = await DocAsync(s.PaymentId);
        dot.Sum(d => d.NetAmount).Should().Be(0m);
        but.Where(e => e.IsDebit && e.Account.OwnerType == AccountType.Tax).Sum(e => e.Amount).Should().Be(55.55m);
        but.Where(e => e.IsDebit && e.Account.OwnerType == AccountType.Platform).Sum(e => e.Amount).Should().Be(1_111m - 55.55m);
        but.Where(e => !e.IsDebit && e.Account.OwnerType == AccountType.Gateway).Sum(e => e.Amount).Should().Be(1_111m);
    }
}
