using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.ValueObjects;
using MusicLounge.Infrastructure.Persistence;
using MusicLounge.Tests.Integration.Helpers;
using MusicLoungeVenue = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Tests.Integration.CF7;

/// <summary>
/// MLACP-359. Báo cáo doanh thu của phòng trà từng cộng toàn bộ Gross donate vào doanh thu. Phần
/// nghệ sĩ được nhận (mặc định 88% Gross) là tiền phòng trà thu hộ và phải chuyển đi — theo VAS 14,
/// khoản thu hộ bên thứ ba không phải doanh thu. Báo cáo này là cái chủ phòng trà xuất ra nộp kế
/// toán, nên con số sai ở đây là doanh thu khai sai.
///
/// <para>Hai donate: một có tỉ lệ chốt lúc VNPay xác nhận, một từ trước khi có cột chốt (dùng tỉ
/// lệ dự phòng trong system_config) — đúng hai nhánh ConfirmDonationPaid dùng khi ghi sổ chặng 2.
/// Giá trị mong đợi tính độc lập trong test.</para>
/// </summary>
[Collection("Integration")]
public sealed class DonationRevenueReportTests
{
    private const decimal SnapshotGross = 100_000m;
    private const decimal LegacyGross = 50_000m;

    private readonly ApiFactory _factory;

    public DonationRevenueReportTests(ApiFactory factory) => _factory = factory;

    private sealed record Wrapped<T>(T Data);

    private sealed record EventSlice(
        int ShowId, decimal DonationRevenue, decimal TotalRevenue, decimal DonationCollectedForPerformers);

    private sealed record MonthSlice(decimal DonationRevenue, decimal DonationCollectedForPerformers);

    private sealed record ReportSlice(
        decimal TotalDonationRevenue,
        decimal TotalDonationCollectedForPerformers,
        decimal GrandTotal,
        List<EventSlice> ByEvent,
        List<MonthSlice> ByMonth);

    private async Task<(int OwnerId, int LoungeId, int ShowId, decimal ForPerformers, decimal OwnerShare)> SeedAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var config = scope.ServiceProvider.GetRequiredService<ISystemConfigService>();

        var owner = new User { Email = $"donrev-{Guid.NewGuid():N}@test.com", FullName = "Donation Report Owner" };
        db.Users.Add(owner);
        await db.SaveChangesAsync();

        var lounge = new MusicLoungeVenue
        {
            OwnerId = owner.Id, Name = $"DonRevLounge-{Guid.NewGuid():N}"[..30], Status = LoungeStatus.Approved,
            Address = new VenueAddress { Street = "1 Test St", District = "1", City = "HCM" }
        };
        db.Lounges.Add(lounge);
        await db.SaveChangesAsync();

        var start = DateTimeOffset.UtcNow.AddHours(-3);
        var show = new LoungeShow
        {
            LoungeId = lounge.Id, Name = $"DonRevShow-{Guid.NewGuid():N}", Description = "test",
            Format = LoungeShowFormat.Offline, Status = LoungeShowStatus.Ended,
            ScheduledStart = start, ScheduledEnd = start.AddHours(2), VcpmcRoyaltyReference = "VCPMC-TEST"
        };
        var performer = new Performer { Name = $"DonRevArtist-{Guid.NewGuid():N}"[..25], CreatedByUserId = owner.Id };
        db.Add(show);
        db.Add(performer);
        await db.SaveChangesAsync();

        var performance = new Performance { LoungeShowId = show.Id, PerformerId = performer.Id };
        db.Add(performance);
        await db.SaveChangesAsync();

        var confirmedAt = DateTimeOffset.UtcNow.AddHours(-2);
        db.Add(new Donation
        {
            PerformanceId = performance.Id, Gross = SnapshotGross, Net = 90_000m,
            Status = DonationStatus.PendingOwnerAck, PerformerShareRateSnapshot = 0.88m,
            PaymentConfirmedAt = confirmedAt, CreatedAt = confirmedAt,
            GatewayRef = $"DON-{Guid.NewGuid():N}"
        });
        db.Add(new Donation
        {
            PerformanceId = performance.Id, Gross = LegacyGross, Net = 45_000m,
            Status = DonationStatus.OwnerReceived, PerformerShareRateSnapshot = null,
            PaymentConfirmedAt = confirmedAt, CreatedAt = confirmedAt,
            GatewayRef = $"DON-{Guid.NewGuid():N}"
        });
        // Chưa thu được tiền — không thuộc báo cáo, dù ở dạng nào.
        db.Add(new Donation
        {
            PerformanceId = performance.Id, Gross = 30_000m, Net = 27_000m,
            Status = DonationStatus.PendingPayment, CreatedAt = confirmedAt,
            GatewayRef = $"DON-{Guid.NewGuid():N}"
        });
        await db.SaveChangesAsync();

        var fallback = await config.GetDecimalAsync(
            ConfigKeys.DonationPerformerShareRate, 0.88m, CancellationToken.None);
        var forPerformers = Math.Round(SnapshotGross * 0.88m, 2) + Math.Round(LegacyGross * fallback, 2);
        var ownerShare = SnapshotGross + LegacyGross - forPerformers;

        return (owner.Id, lounge.Id, show.Id, forPerformers, ownerShare);
    }

    [Fact]
    public async Task PerformersShare_IsReportedAsCollectedForThem_NotAsTheVenuesRevenue()
    {
        var (ownerId, loungeId, showId, forPerformers, ownerShare) = await SeedAsync();
        var client = _factory.CreateAuthenticatedClient(ownerId, "Owner", loungeId);

        var res = await client.GetAsync($"/api/v1/analytics/revenue-report?loungeId={loungeId}");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var report = (await res.Content.ReadFromJsonAsync<Wrapped<ReportSlice>>())!.Data;

        report.TotalDonationRevenue.Should().Be(ownerShare,
            "chỉ phần phòng trà giữ lại mới là doanh thu của phòng trà");
        report.TotalDonationCollectedForPerformers.Should().Be(forPerformers);
        report.GrandTotal.Should().Be(ownerShare,
            "phòng trà mới, không có vé hay F&B — tổng doanh thu chỉ còn phần donate của chính nó");
        (report.TotalDonationRevenue + report.TotalDonationCollectedForPerformers)
            .Should().Be(SnapshotGross + LegacyGross,
                "tách ra chứ không làm mất: hai phần cộng lại phải đúng bằng tiền khán giả đã trả");

        var show = report.ByEvent.Should().ContainSingle().Subject;
        show.ShowId.Should().Be(showId);
        show.DonationRevenue.Should().Be(ownerShare);
        show.TotalRevenue.Should().Be(ownerShare);
        show.DonationCollectedForPerformers.Should().Be(forPerformers);

        report.ByMonth.Sum(m => m.DonationRevenue).Should().Be(ownerShare);
        report.ByMonth.Sum(m => m.DonationCollectedForPerformers).Should().Be(forPerformers);
    }

    [Fact]
    public async Task ExportedCsv_KeepsCollectedForPerformersOutOfTheTotal()
    {
        // File này là cái chủ phòng trà nộp cho kế toán — phải nói cùng một điều với màn hình.
        var (ownerId, loungeId, _, forPerformers, ownerShare) = await SeedAsync();
        var client = _factory.CreateAuthenticatedClient(ownerId, "Owner", loungeId);

        var res = await client.GetAsync($"/api/v1/analytics/revenue-report/export?loungeId={loungeId}");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var lines = (await res.Content.ReadAsStringAsync()).Split('\n').Select(l => l.TrimEnd('\r')).ToList();

        decimal ValueOf(string label)
        {
            var line = lines.Single(l => l.StartsWith(label + ",", StringComparison.Ordinal));
            return decimal.Parse(line[(label.Length + 1)..], CultureInfo.InvariantCulture);
        }

        ValueOf("Tổng cộng").Should().Be(ownerShare);
        ValueOf("Donate (phần phòng trà)").Should().Be(ownerShare);
        ValueOf("Donate thu hộ nghệ sĩ (không phải doanh thu)").Should().Be(forPerformers);
    }
}
