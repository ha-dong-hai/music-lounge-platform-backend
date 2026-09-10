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
/// MLACP-355. <c>ScheduledEnd</c> không bắt buộc — validator và cổng nộp duyệt đều cho để trống. Khi
/// trống, chốt thời lượng D16 từng so với mặc định 4 giờ: một buổi diễn 2 giờ diễn trọn vẹn bị tính là
/// giao 50%, dưới ngưỡng 0.70, và tranche cuối của phòng trà bị giữ chờ Admin dù không có gì sai.
///
/// <para>Chính <c>ShowCompletion.IsAcceptable</c> đã ghi nguyên tắc: không kết luận được thì coi là đạt
/// — giữ mọi khoản chi trả cuối cùng chỉ vì thiếu dữ liệu sẽ giam tiền của mọi phòng trà làm ăn tử tế.</para>
/// </summary>
[Collection("Integration")]
public sealed class UndeclaredShowLengthPayoutTests
{
    private readonly ApiFactory _factory;

    public UndeclaredShowLengthPayoutTests(ApiFactory factory) => _factory = factory;

    private async Task<int> SeedDueFinalTrancheAsync(
        int? declaredLengthHours, bool started, int ranHours = 2, int startedHoursAgo = 10)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var start = DateTimeOffset.UtcNow.AddHours(-startedHoursAgo);
        var show = new LoungeShow
        {
            LoungeId = SeedHelper.LoungeId,
            Name = $"Length {Guid.NewGuid():N}"[..16],
            Description = "Integration test show",
            Format = LoungeShowFormat.Offline,
            Status = LoungeShowStatus.Ended,
            ScheduledStart = start,
            ScheduledEnd = declaredLengthHours is int hours ? start.AddHours(hours) : null,
            ActualStart = started ? start : null,
            // Chưa từng bắt đầu mà đã Ended: job tự đóng buổi diễn quá hạn.
            ActualEnd = start.AddHours(ranHours)
        };
        db.LoungeShows.Add(show);
        await db.SaveChangesAsync();

        var payment = new Payment
        {
            OrderId = $"MLACP355-{Guid.NewGuid():N}"[..30],
            PayerId = SeedHelper.AudienceId,
            GrossAmount = 1_000_000m,
            NetAmount = 880_000m,
            Status = PaymentStatus.Confirmed,
            ReferenceType = "TicketHold",
            ReferenceId = "0",
            TransactionId = $"D{Guid.NewGuid():N}"[..16],
            PaidAt = start.AddDays(-3),
            CreatedAt = start.AddDays(-3)
        };
        db.Add(payment);
        await db.SaveChangesAsync();

        // SettlementReleaseJob tìm buổi diễn của một khoản quyết toán qua vé.
        db.Add(new Ticket
        {
            Id = Guid.NewGuid(),
            BuyerId = SeedHelper.AudienceId,
            PriceId = SeedHelper.TicketPriceId,
            TierId = SeedHelper.TicketTierId,
            ShowId = show.Id,
            PaymentId = payment.Id,
            Status = TicketStatus.Used,
            PurchaseChannel = PurchaseChannel.Online,
            CreatedAt = start.AddDays(-3)
        });

        var payoutAccountId = await db.Set<BankAccount>()
            .Where(a => a.OwnerType == BankAccountOwnerType.Lounge && a.OwnerId == SeedHelper.LoungeId && a.IsDefault)
            .Select(a => (int?)a.Id)
            .FirstAsync();

        var settlement = new Settlement
        {
            OwnerId = SeedHelper.OwnerId,
            PaymentId = payment.Id,
            BankAccountId = payoutAccountId,
            ReleaseType = SettlementReleaseType.Final30,
            GrossAmount = 1_000_000m,
            PreRateApplied = 0.70m,
            PostRateApplied = 0.30m,
            NetAmount = 264_000m,
            Status = SettlementStatus.Scheduled,
            ScheduledAt = DateTimeOffset.UtcNow.AddMinutes(-5),
            CreatedAt = start.AddDays(-3)
        };
        db.Add(settlement);
        await db.SaveChangesAsync();

        return settlement.Id;
    }

    private async Task<SettlementStatus> RunReleaseAsync(int settlementId)
    {
        using (var scope = _factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<SettlementReleaseJob>()
                .ExecuteAsync(new JobCancellationToken(false));
        }

        using var readScope = _factory.Services.CreateScope();
        var db = readScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return (await db.Settlements.AsNoTracking().SingleAsync(s => s.Id == settlementId)).Status;
    }

    [Fact]
    public async Task KhongKhaiBaoGioKetThucThiKhongGiuTrancheCuoiVoCo()
    {
        var settlementId = await SeedDueFinalTrancheAsync(declaredLengthHours: null, started: true, ranHours: 2);

        (await RunReleaseAsync(settlementId)).Should().Be(SettlementStatus.Released,
            "không có giờ kết thúc khai báo thì không có 'thời lượng đã bán' nào để nói buổi diễn ngắn — " +
            "trước đây bị so với mặc định 4 giờ và bị giữ ở 50%");
    }

    [Fact]
    public async Task CoKhaiBaoVaDienNganHonThiVanGiuChoAdmin()
    {
        // Đối chứng: khai báo 4 giờ mà chỉ diễn 2 giờ — chốt D16 phải còn nguyên tác dụng.
        var settlementId = await SeedDueFinalTrancheAsync(declaredLengthHours: 4, started: true, ranHours: 2);

        (await RunReleaseAsync(settlementId)).Should().Be(SettlementStatus.PendingReview);
    }

    [Fact]
    public async Task ChuaTungBatDauThiVanGiuDuKhongKhaiBaoGioKetThuc()
    {
        // "Chưa từng bắt đầu" là bằng chứng thật, không phải thiếu dữ liệu — phải đứng trước quy tắc mới.
        var settlementId = await SeedDueFinalTrancheAsync(declaredLengthHours: null, started: false, ranHours: 4);

        (await RunReleaseAsync(settlementId)).Should().Be(SettlementStatus.PendingReview);
    }
}
