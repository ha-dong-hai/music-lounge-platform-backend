using FluentAssertions;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MusicLounge.Application.Common;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Infrastructure.Jobs;
using MusicLounge.Infrastructure.Persistence;
using MusicLounge.Tests.Integration.Helpers;

namespace MusicLounge.Tests.Integration.Payments;

/// <summary>
/// MLACP-336. Một buổi diễn offline <b>không hề diễn ra</b> vẫn chi trả đủ 100% cho phòng trà.
///
/// <para>Đường đi: show ở <c>Published</c>, không ai gọi <c>EndLoungeShow</c>. Sáu tiếng sau giờ kết
/// thúc dự kiến, <c>AutoEndStaleShowsJob</c> đánh <c>Ended</c>, đặt <c>ActualEnd</c>, và
/// <b>cố ý để <c>ActualStart</c> null</b> — lập luận của job đúng: không ai xác nhận buổi diễn đã bắt
/// đầu, bịa ra giờ bắt đầu sẽ biến "không kết luận được" thành một tỉ lệ nguỵ tạo. Rồi ở đầu bên
/// kia, <c>+48h</c> Partial70 giải ngân (tranche này <b>không có chốt hoàn thành nào cả</b>), và
/// <c>+14 ngày</c> Final30 giải ngân vì <c>ActualStart is null</c> bị đọc là "không kết luận được,
/// coi như bình thường".</para>
///
/// <para><b>Cả hai đoạn code đều đúng khi đọc riêng lẻ.</b> Chỗ hỏng nằm ở chỗ chúng gặp nhau: chú
/// thích tại chỗ quyết định viện lý do *"no tracking mechanism wired up yet"* — mệnh đề đó đúng vào
/// lúc nó được viết, khi một show không ai đóng thì cả hai mốc đều null. Job tự đóng ra đời đã tạo
/// ra một cặp thứ ba mà đoạn code cũ không lường tới.</para>
///
/// <para><b>Cặp (null, có) không mơ hồ.</b> <c>EndLoungeShow</c> bắt buộc <c>Ongoing</c>, mà
/// <c>Ongoing</c> chỉ đến từ <c>StartLoungeShow</c> / <c>StartLivestream</c> — hai chỗ duy nhất
/// trong toàn bộ mã nguồn ghi <c>ActualStart</c>. Nên mọi buổi diễn do <b>người</b> kết thúc đều có
/// <c>ActualStart</c>.</para>
/// </summary>
[Collection("Integration")]
public sealed class ShowNeverStartedPayoutTests
{
    private readonly ApiFactory _factory;

    public ShowNeverStartedPayoutTests(ApiFactory factory) => _factory = factory;

    /// <param name="actualStart">
    /// <c>null</c> tái hiện buổi diễn bị job tự đóng mà chưa từng bắt đầu.
    /// </param>
    private async Task<int> DueSettlementAsync(
        SettlementReleaseType releaseType,
        DateTimeOffset? actualStart,
        DateTimeOffset? actualEnd,
        LoungeShowStatus status = LoungeShowStatus.Ended)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var scheduledStart = DateTimeOffset.UtcNow.AddDays(-20);

        var show = new LoungeShow
        {
            LoungeId = SeedHelper.LoungeId,
            Name = $"Show {Guid.NewGuid():N}"[..18],
            Status = status,
            ScheduledStart = scheduledStart,
            ScheduledEnd = scheduledStart.AddHours(2),
            ActualStart = actualStart,
            ActualEnd = actualEnd,
            CreatedAt = DateTime.UtcNow
        };
        db.Add(show);
        await db.SaveChangesAsync();

        var payment = new Payment
        {
            OrderId = $"MLACP336-{Guid.NewGuid():N}"[..30],
            PayerId = SeedHelper.AudienceId,
            GrossAmount = 1_000_000m,
            NetAmount = 880_000m,
            Status = PaymentStatus.Confirmed,
            ReferenceType = "TicketHold",
            ReferenceId = "0",
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.Add(payment);
        await db.SaveChangesAsync();

        db.Add(new Ticket
        {
            Id = Guid.NewGuid(),
            BuyerId = SeedHelper.AudienceId,
            PriceId = SeedHelper.TicketPriceId,
            TierId = SeedHelper.TicketTierId,
            ShowId = show.Id,
            PaymentId = payment.Id,
            Status = TicketStatus.Confirmed,
            PurchaseChannel = PurchaseChannel.Online,
            CreatedAt = DateTimeOffset.UtcNow
        });

        var payoutAccountId = await db.Set<BankAccount>()
            .Where(a => a.OwnerType == BankAccountOwnerType.Lounge && a.OwnerId == SeedHelper.LoungeId)
            .Select(a => (int?)a.Id)
            .FirstAsync();

        var settlement = new Settlement
        {
            OwnerId = SeedHelper.OwnerId,
            PaymentId = payment.Id,
            BankAccountId = payoutAccountId,
            ReleaseType = releaseType,
            GrossAmount = 1_000_000m,
            PreRateApplied = 0.70m,
            PostRateApplied = 0.30m,
            NetAmount = releaseType == SettlementReleaseType.Partial70 ? 616_000m : 264_000m,
            Status = SettlementStatus.Scheduled,
            ScheduledAt = DateTimeOffset.UtcNow.AddDays(-1),   // da den han
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.Add(settlement);
        await db.SaveChangesAsync();

        return settlement.Id;
    }

    private async Task RunJobAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var job = scope.ServiceProvider.GetRequiredService<SettlementReleaseJob>();
        await job.ExecuteAsync(new JobCancellationToken(false));
    }

    private async Task<SettlementStatus> StatusAsync(int settlementId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return (await db.Settlements.FindAsync(settlementId))!.Status;
    }

    // ── Buổi diễn chưa từng bắt đầu: chặn CẢ HAI tranche ────────────────────

    [Fact]
    public async Task ChuaTungBatDauThiTrancheCuoiKhongDuocTuGiaiNgan()
    {
        var settlementId = await DueSettlementAsync(
            SettlementReleaseType.Final30,
            actualStart: null,
            actualEnd: DateTimeOffset.UtcNow.AddDays(-20).AddHours(2));

        await RunJobAsync();

        (await StatusAsync(settlementId)).Should().Be(SettlementStatus.PendingReview,
            "khán giả đã trả tiền cho một buổi diễn có thể chưa từng diễn ra");
    }

    [Fact]
    public async Task ChuaTungBatDauThiTrancheDauCungKhongDuocTuGiaiNgan()
    {
        // Đây là nửa mà chốt D16 cũ không chạm tới: Partial70 không có chốt hoàn thành nào cả, nên
        // 70% tiền vẫn đi ra sau 48 tiếng dù buổi diễn chưa từng bắt đầu.
        var settlementId = await DueSettlementAsync(
            SettlementReleaseType.Partial70,
            actualStart: null,
            actualEnd: DateTimeOffset.UtcNow.AddDays(-20).AddHours(2));

        await RunJobAsync();

        (await StatusAsync(settlementId)).Should().Be(SettlementStatus.PendingReview,
            "trả nhanh 70% cho một thứ không tồn tại không phải là trả nhanh, chỉ là trả sai sớm hơn");
    }

    [Fact]
    public async Task AdminPhaiBietDuocVISAOKhoanBiGiuLai()
    {
        var settlementId = await DueSettlementAsync(
            SettlementReleaseType.Final30,
            actualStart: null,
            actualEnd: DateTimeOffset.UtcNow.AddDays(-20).AddHours(2));

        await RunJobAsync();

        var res = await _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin")
            .GetAsync("/api/v1/admin/settlements/pending-review?page=1&pageSize=50");
        var body = await res.Content.ReadAsStringAsync();

        body.Should().Contain("NeverStarted",
            "tỉ lệ null vừa có nghĩa 'chưa từng bắt đầu' vừa có nghĩa 'không đo được' — Admin phải " +
            "phân biệt được, vì một cái kéo theo hoàn tiền cho khách còn cái kia thì không");
    }

    [Fact]
    public async Task BuoiDienKetOPublishedQuaGioCungPhaiBiGiuLai()
    {
        // MLACP-338. Chốt cũ dùng ShowCompletion.Evaluate, vốn đòi buổi diễn đã được ĐÓNG lại
        // (ActualEnd có giá trị). Một buổi diễn kẹt ở Published vì AutoEndStaleShowsJob chưa chạy
        // sẽ rơi vào "không kết luận được" và giải ngân bình thường — phòng trà được trả tiền cho
        // một buổi diễn chưa từng bắt đầu.
        //
        // Đây cũng chính là thứ khiến cửa hoàn tiền của người mua không thể mở an toàn: nếu tiền đã
        // ra khỏi escrow thì hoàn tiền phải truy thu từ tài khoản chủ phòng trà. Hai chốt phải phủ
        // đúng cùng một tập hợp.
        var settlementId = await DueSettlementAsync(
            SettlementReleaseType.Final30,
            actualStart: null,
            actualEnd: null,
            status: LoungeShowStatus.Published);

        await RunJobAsync();

        (await StatusAsync(settlementId)).Should().Be(SettlementStatus.PendingReview,
            "job tự đóng chưa chạy không có nghĩa là buổi diễn đã diễn ra — codebase này đã năm lần " +
            "có job chết lặng lẽ vì quên đăng ký DI");
    }

    // ── Buổi diễn chạy bình thường vẫn phải được trả tiền ───────────────────

    [Fact]
    public async Task BuoiDienChayDuThoiLuongVanDuocGiaiNganBinhThuong()
    {
        // Chiều ngược lại, và là chiều dễ làm hỏng nhất: siết chốt không được giam tiền của những
        // phòng trà làm ăn tử tế.
        var start = DateTimeOffset.UtcNow.AddDays(-20);
        var settlementId = await DueSettlementAsync(
            SettlementReleaseType.Final30,
            actualStart: start,
            actualEnd: start.AddHours(2));

        await RunJobAsync();

        (await StatusAsync(settlementId)).Should().Be(SettlementStatus.Released);
    }

    [Fact]
    public async Task KhongXacDinhDuocBuoiDienThiVanPhaiGiaiNgan()
    {
        // MLACP-338 làm quy tắc sắc hơn bài này lúc đầu: cặp (null, null) từng được coi là "thiếu
        // bằng chứng" và vẫn giải ngân. Nhưng một tranche chỉ ĐẾN HẠN ở giờ-kết-thúc cộng 48 tiếng,
        // nên nếu nó đến hạn mà buổi diễn chưa từng được bắt đầu thì đó là bằng chứng, không phải
        // thiếu bằng chứng — dù buổi diễn có được đóng lại hay không.
        //
        // Phần còn đúng của nguyên tắc cũ nằm ở đây: khi thật sự không xác định được buổi diễn nào
        // đứng sau giao dịch, vẫn phải giải ngân. Chặn mọi khoản chi chỉ vì thiếu dữ liệu sẽ giam
        // tiền của tất cả những phòng trà làm ăn tử tế.
        int settlementId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // Thanh toán không gắn với vé nào — không lần ra được buổi diễn.
            var payment = new Payment
            {
                OrderId = $"MLACP338N-{Guid.NewGuid():N}"[..30],
                PayerId = SeedHelper.AudienceId,
                GrossAmount = 1_000_000m,
                NetAmount = 880_000m,
                Status = PaymentStatus.Confirmed,
                ReferenceType = "TicketHold",
                ReferenceId = "0",
                CreatedAt = DateTimeOffset.UtcNow
            };
            db.Add(payment);
            await db.SaveChangesAsync();

            var payoutAccountId = await db.Set<BankAccount>()
                .Where(a => a.OwnerType == BankAccountOwnerType.Lounge && a.OwnerId == SeedHelper.LoungeId)
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
                ScheduledAt = DateTimeOffset.UtcNow.AddDays(-1),
                CreatedAt = DateTimeOffset.UtcNow
            };
            db.Add(settlement);
            await db.SaveChangesAsync();
            settlementId = settlement.Id;
        }

        await RunJobAsync();

        (await StatusAsync(settlementId)).Should().Be(SettlementStatus.Released);
    }

    // ── Chốt thuần trên phép tính ───────────────────────────────────────────

    [Fact]
    public void PhanBietBaTrangThai()
    {
        var start = DateTimeOffset.UtcNow.AddDays(-1);

        LoungeShow Show(DateTimeOffset? actualStart, DateTimeOffset? actualEnd) => new()
        {
            ScheduledStart = start,
            ScheduledEnd = start.AddHours(2),
            ActualStart = actualStart,
            ActualEnd = actualEnd
        };

        ShowCompletion.Evaluate(Show(null, null)).Verdict
            .Should().Be(ShowCompletionVerdict.Unknown, "chưa đóng thì chưa biết gì");

        ShowCompletion.Evaluate(Show(null, start.AddHours(2))).Verdict
            .Should().Be(ShowCompletionVerdict.NeverStarted, "đã đóng mà chưa từng bắt đầu");

        ShowCompletion.Evaluate(Show(start, start.AddHours(2))).Verdict
            .Should().Be(ShowCompletionVerdict.Measured);

        // Điểm mấu chốt của MLACP-336: trước đây hai cặp đầu gộp làm một và đều được coi là đạt.
        ShowCompletion.IsAcceptable(ShowCompletion.Evaluate(Show(null, null)), 0.70m)
            .Should().BeTrue();
        ShowCompletion.IsAcceptable(ShowCompletion.Evaluate(Show(null, start.AddHours(2))), 0.70m)
            .Should().BeFalse();
    }
}
