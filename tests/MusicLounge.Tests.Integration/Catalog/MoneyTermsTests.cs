using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Infrastructure.Persistence;
using MusicLounge.Tests.Integration.Helpers;

namespace MusicLounge.Tests.Integration.Catalog;

/// <summary>
/// MLACP-625. Biểu phí và điều khoản tiền công khai (GET /catalog/money-terms) phải nói ĐÚNG con số đang áp dụng, và
/// phải tự ghi "cập nhật lúc nào, đổi từ bao nhiêu sang bao nhiêu" mỗi khi Admin đổi cấu hình — không ai phải nhớ sửa
/// tay một dòng ngày tháng.
///
/// system_config dùng chung giữa các test: mọi ca đổi giá trị đều trả lại trong finally và xoá cache.
/// </summary>
[Collection("Integration")]
public sealed class MoneyTermsTests
{
    private readonly ApiFactory _factory;

    public MoneyTermsTests(ApiFactory factory) => _factory = factory;

    private sealed record Envelope<T>(bool Success, T Data);
    private sealed record Ticket(decimal PlatformCommissionRate, decimal VatRate, decimal PersonalIncomeTaxRate,
        int HoldMinutes, int HoldMaxQuantity, bool WalkInCashGoesThroughPlatform);
    private sealed record Refund(int ReviewHours, int AutoApproveAfterHours, int GatewayRefundWindowDays);
    private sealed record Donation(decimal PerformerShareRate, decimal PlatformCommissionRate, decimal VatRate,
        decimal PersonalIncomeTaxRate, decimal VenueShareRate, int VenuePayoutDays, int VenueWarningDays,
        bool Refundable, decimal MaxAmount);
    private sealed record Settlement(int FirstTrancheHoursAfterShow, int FinalTrancheDaysAfterShow,
        decimal NewVenueFirstTrancheRate, decimal StandardFirstTrancheRate, decimal PremiumFirstTrancheRate);
    private sealed record Change(string Key, string? OldValue, string NewValue, DateTimeOffset EffectiveFrom);
    private sealed record Terms(Ticket Ticket, Refund Refund, Donation Donation, Settlement Settlement,
        DateTimeOffset? UpdatedAt, List<Change> Changes);

    private async Task<Terms> GetAsync()
    {
        // Không đăng nhập: khách phải đọc được điều khoản TRƯỚC khi có tài khoản.
        var res = await _factory.CreateClient().GetAsync("/api/v1/catalog/money-terms");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await res.Content.ReadFromJsonAsync<Envelope<Terms>>())!.Data;
    }

    private HttpClient Admin() => _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin");

    private Task<HttpResponseMessage> PutAsync(string key, string value, string note)
        => Admin().PutAsJsonAsync($"/api/v1/admin/system-config/{key}", new { ConfigValue = value, Note = note });

    private async Task<string> CurrentAsync(string key)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return (await db.SystemConfigs.AsNoTracking().SingleAsync(c => c.ConfigKey == key)).ConfigValue;
    }

    /// <summary>Trả giá trị về như cũ bằng đường thẳng vào DB (không sinh thêm dòng lịch sử) rồi xoá cache.</summary>
    private async Task RestoreAsync(string key, string value)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.SystemConfigs.SingleAsync(c => c.ConfigKey == key)).ConfigValue = value;
        await db.SaveChangesAsync();
        scope.ServiceProvider.GetRequiredService<ISystemConfigService>().Invalidate(key);
    }

    [Fact]
    public async Task KhongDangNhap_DocDuocBieuPhi_VaTongTienUngHoDungMotTram()
    {
        // Seed để thuế TNCN = 0, khi đó quên trừ nó vẫn ra đúng 100%. Đặt thẳng vào DB cặp 0.86 / 0.02 (cấu hình đang
        // chạy thật từ 04/10/2026) để phép cộng bên dưới thật sự kiểm phần phòng trà giữ lại.
        var gocPhanNgheSi = await CurrentAsync(ConfigKeys.DonationPerformerShareRate);
        var gocTncn = await CurrentAsync(ConfigKeys.PersonalIncomeTaxRate);
        try
        {
        await RestoreAsync(ConfigKeys.DonationPerformerShareRate, "0.86");
        await RestoreAsync(ConfigKeys.PersonalIncomeTaxRate, "0.02");
        var t = await GetAsync();
        t.Donation.PersonalIncomeTaxRate.Should().Be(0.02m);
        t.Donation.VenueShareRate.Should().Be(0.02m, "100% − 86% nghệ sĩ − 5% phí − 5% GTGT − 2% TNCN");

        t.Ticket.PlatformCommissionRate.Should().Be(decimal.Parse(await CurrentAsync(ConfigKeys.PlatformCommissionRate),
            System.Globalization.CultureInfo.InvariantCulture));
        t.Ticket.HoldMinutes.Should().BeGreaterThan(0);
        t.Refund.AutoApproveAfterHours.Should().BeGreaterThan(t.Refund.ReviewHours,
            "mốc tự duyệt = hạn xử lý + khoảng chờ thêm, không thể sớm hơn chính hạn xử lý");
        t.Donation.Refundable.Should().BeFalse();

        // Khách đọc năm con số này cạnh nhau; cộng không ra 100% là trang đang nói sai với chính nó.
        (t.Donation.PerformerShareRate + t.Donation.PlatformCommissionRate + t.Donation.VatRate
         + t.Donation.PersonalIncomeTaxRate + t.Donation.VenueShareRate).Should().Be(1m);
        }
        finally
        {
            await RestoreAsync(ConfigKeys.DonationPerformerShareRate, gocPhanNgheSi);
            await RestoreAsync(ConfigKeys.PersonalIncomeTaxRate, gocTncn);
        }
    }

    [Fact]
    public async Task AdminDoiMotThamSoTien_BieuPhiDoiTheo_VaNhatKyGhiMucCuMucMoiCungThoiDiem()
    {
        const string key = ConfigKeys.TicketHoldMinutes;
        var goc = await CurrentAsync(key);
        var giua = goc == "20" ? "22" : "20";
        var moi = goc == "25" ? "27" : "25";
        var truoc = await GetAsync();
        var mocTruoc = DateTimeOffset.UtcNow.AddSeconds(-2);
        try
        {
            // Đổi HAI lần: một dòng lịch sử thì xếp xuôi hay ngược cũng như nhau, không chứng minh được thứ tự.
            (await PutAsync(key, giua, "MLACP-625: kiểm nhật ký thay đổi điều khoản tiền (lần 1)")).StatusCode
                .Should().Be(HttpStatusCode.NoContent);
            await Task.Delay(30);
            (await PutAsync(key, moi, "MLACP-625: kiểm nhật ký thay đổi điều khoản tiền (lần 2)")).StatusCode
                .Should().Be(HttpStatusCode.NoContent);

            var sau = await GetAsync();
            sau.Ticket.HoldMinutes.Should().Be(int.Parse(moi), "biểu phí phải đọc giá trị đang chạy, không gõ cứng");

            sau.Changes.Should().NotBeEmpty();
            var dau = sau.Changes[0];
            dau.Key.Should().Be(key, "nhật ký xếp mới nhất lên đầu");
            dau.OldValue.Should().Be(giua);
            dau.NewValue.Should().Be(moi);
            dau.EffectiveFrom.Should().BeAfter(mocTruoc);
            sau.UpdatedAt.Should().Be(dau.EffectiveFrom, "ngày cập nhật là thời điểm của thay đổi gần nhất");
            sau.Changes[1].NewValue.Should().Be(giua);
            sau.Changes[1].OldValue.Should().Be(goc);
            sau.Changes.Count.Should().Be(truoc.Changes.Count + 2);
        }
        finally
        {
            await RestoreAsync(key, goc);
        }
    }

    [Fact]
    public async Task ThamSoKhongPhaiTien_KhongLotVaoNhatKyCongKhai()
    {
        // Nhật ký công khai chỉ gồm khoá tiền: một khoá vận hành nội bộ (số giờ SLA kiểm duyệt) đổi thì KHÔNG hiện.
        const string key = ConfigKeys.ModerationSlaHours;
        var goc = await CurrentAsync(key);
        var moi = goc == "30" ? "36" : "30";
        var truoc = await GetAsync();
        try
        {
            (await PutAsync(key, moi, "MLACP-625: khoá ngoài phạm vi điều khoản tiền")).StatusCode
                .Should().Be(HttpStatusCode.NoContent);

            var sau = await GetAsync();
            sau.Changes.Should().NotContain(c => c.Key == key);
            sau.Changes.Count.Should().Be(truoc.Changes.Count);
            sau.UpdatedAt.Should().Be(truoc.UpdatedAt);
        }
        finally
        {
            await RestoreAsync(key, goc);
        }
    }
}
