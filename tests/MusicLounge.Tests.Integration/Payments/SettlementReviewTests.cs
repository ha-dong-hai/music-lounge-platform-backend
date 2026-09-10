using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Infrastructure.Persistence;
using MusicLounge.Tests.Integration.Helpers;

namespace MusicLounge.Tests.Integration.Payments;

/// <summary>
/// MLACP-335. Chốt D16 giữ lại tranche cuối khi tỉ lệ thời lượng thật / dự kiến không đạt ngưỡng,
/// và doc của <c>SettlementReleaseJob</c> ghi rõ *"parked as PendingReview for Admin to decide"*.
///
/// <para><b>Nhưng Admin không có cách nào để quyết.</b> Đã tìm toàn bộ <c>src/</c>: không một
/// endpoint, command, hay query nào cho Settlement tồn tại. Job lại chỉ lấy <c>Scheduled</c> nên
/// không bao giờ ngó lại. Khoản tiền nằm đó vĩnh viễn — trong khi <c>GetMyEarnings</c> vẫn đếm cả
/// <c>PendingReview</c> vào mục Owner sắp nhận được, nên Owner thấy một khoản họ sẽ không bao giờ
/// nhận, không kèm lời giải thích nào.</para>
///
/// <para><b>Đây là lỗi đã sống hôm nay</b>, không cần kịch bản lạ: Owner bắt đầu buổi diễn lúc
/// 20:00, kết thúc 20:30, trong khi lịch là 20:00–22:00 thì tỉ lệ 0.25 &lt; 0.70. Buổi diễn bị cắt
/// ngắn vì sự cố là chuyện bình thường.</para>
/// </summary>
[Collection("Integration")]
public sealed class SettlementReviewTests
{
    private readonly ApiFactory _factory;

    public SettlementReviewTests(ApiFactory factory) => _factory = factory;

    private sealed record ReviewBody(string Decision, string Note);

    private async Task<(int PaymentId, int SettlementId)> ParkedSettlementAsync(
        bool withBankAccount = true, decimal netAmount = 300_000m)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var payment = new Payment
        {
            OrderId = $"MLACP335-{Guid.NewGuid():N}"[..30],
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

        int? payoutAccountId = null;
        if (withBankAccount)
        {
            payoutAccountId = await db.Set<BankAccount>()
                .Where(a => a.OwnerType == BankAccountOwnerType.Lounge && a.OwnerId == SeedHelper.LoungeId)
                .Select(a => (int?)a.Id)
                .FirstAsync();
        }

        var settlement = new Settlement
        {
            OwnerId = SeedHelper.OwnerId,
            PaymentId = payment.Id,
            BankAccountId = payoutAccountId,
            ReleaseType = SettlementReleaseType.Final30,
            GrossAmount = 1_000_000m,
            PreRateApplied = 0.70m,
            PostRateApplied = 0.30m,
            NetAmount = netAmount,
            Status = SettlementStatus.PendingReview,
            ScheduledAt = DateTimeOffset.UtcNow.AddDays(-1),
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.Add(settlement);
        await db.SaveChangesAsync();

        return (payment.Id, settlement.Id);
    }

    private Task<HttpResponseMessage> ReviewAsync(int settlementId, string decision, string note = "Da doi soat")
        => _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin")
            .PostAsJsonAsync($"/api/v1/admin/settlements/{settlementId}/review", new ReviewBody(decision, note));

    private async Task<Settlement> ReloadAsync(int settlementId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return (await db.Settlements.FindAsync(settlementId))!;
    }

    // ── Đường ra tồn tại và đi được cả hai hướng ─────────────────────────────

    [Fact]
    public async Task AdminDuyetChiTraThiKhoanDuocGiaiNganVaGhiSoCai()
    {
        var (_, settlementId) = await ParkedSettlementAsync();

        var res = await ReviewAsync(settlementId, "Release");

        res.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var settlement = await ReloadAsync(settlementId);
        settlement.Status.Should().Be(SettlementStatus.Released);
        settlement.LedgerJournalId.Should().NotBeNullOrEmpty(
            "giải ngân mà không ghi sổ cái thì tiền đi mà không có dấu vết kế toán");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var lines = await db.LedgerEntries
            .Where(e => e.JournalId == settlement.LedgerJournalId)
            .ToListAsync();
        lines.Should().HaveCount(2, "một bút toán nợ Platform và một bút toán có cho chủ phòng trà");
        lines.Where(l => l.IsDebit).Sum(l => l.Amount)
            .Should().Be(lines.Where(l => !l.IsDebit).Sum(l => l.Amount), "hai vế phải bằng nhau");

        (await db.Notifications.AnyAsync(n =>
            n.UserId == SeedHelper.OwnerId
            && n.Type == NotificationType.SettlementReleased
            && n.ReferenceId == settlementId.ToString()))
            .Should().BeTrue("chủ phòng trà phải biết tiền đã về");
    }

    [Fact]
    public async Task AdminGiuLaiThiKhoanChuyenCancelledVaChuPhongTraBietLyDo()
    {
        var (_, settlementId) = await ParkedSettlementAsync();

        var res = await ReviewAsync(settlementId, "Withhold", "Buoi dien khong dien ra");

        res.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var settlement = await ReloadAsync(settlementId);
        settlement.Status.Should().Be(SettlementStatus.Cancelled,
            "SettlementStatus.Cancelled đã được định nghĩa sẵn nhưng trước MLACP-335 không chỗ nào ghi");
        settlement.LedgerJournalId.Should().BeNull("giữ lại thì không có đồng nào đi đâu cả");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var note = await db.Notifications.FirstOrDefaultAsync(n =>
            n.UserId == SeedHelper.OwnerId
            && n.Type == NotificationType.SettlementWithheld
            && n.ReferenceId == settlementId.ToString());

        note.Should().NotBeNull("giữ tiền của người khác mà không nói gì là không chấp nhận được");
        note!.Body.Should().Contain("Buoi dien khong dien ra", "lý do phải tới được người bị ảnh hưởng");
    }

    // ── Đường giải ngân thứ hai phải có đúng chốt như đường thứ nhất ─────────

    [Fact]
    public async Task KhongDuocChiTraKhiConYeuCauHoanTienDangCho()
    {
        var (paymentId, settlementId) = await ParkedSettlementAsync();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Add(new RefundRequest
            {
                PaymentId = paymentId,
                RequestedBy = SeedHelper.AudienceId,
                Reason = "Show bi cat ngan",
                AmountRequested = 500_000m,
                Status = RefundRequestStatus.Pending,
                CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        var res = await ReviewAsync(settlementId, "Release");

        res.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "job tự động đã hoãn đúng trường hợp này rồi — đường duyệt tay mà bỏ chốt thì chính nó " +
            "là lỗ hổng");
        (await ReloadAsync(settlementId)).Status.Should().Be(SettlementStatus.PendingReview);
    }

    [Fact]
    public async Task KhongDuocChiTraKhiChuaCoTaiKhoanNhanTien()
    {
        var (_, settlementId) = await ParkedSettlementAsync(withBankAccount: false);

        var res = await ReviewAsync(settlementId, "Release");

        res.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await ReloadAsync(settlementId)).Status.Should().Be(SettlementStatus.PendingReview);
    }

    [Fact]
    public async Task QuyetLanThuHaiBiTuChoi()
    {
        var (_, settlementId) = await ParkedSettlementAsync();

        (await ReviewAsync(settlementId, "Release")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var second = await ReviewAsync(settlementId, "Release");

        second.StatusCode.Should().Be(HttpStatusCode.Conflict,
            "sổ cái chỉ ghi thêm chứ không sửa được — giải ngân hai lần phải gỡ bằng bút toán đảo tay");
    }

    [Fact]
    public async Task PhaiGhiLyDoMoiQuyetDuoc()
    {
        var (_, settlementId) = await ParkedSettlementAsync();

        var res = await ReviewAsync(settlementId, "Withhold", note: "");

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── Danh sách phải mang đúng bằng chứng đã giữ khoản đó lại ──────────────

    [Fact]
    public async Task DanhSachChoDuyetHienDungKhoanDangCho()
    {
        var (_, settlementId) = await ParkedSettlementAsync(netAmount: 321_000m);

        var res = await _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin")
            .GetAsync("/api/v1/admin/settlements/pending-review?page=1&pageSize=50");

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await res.Content.ReadAsStringAsync();
        body.Should().Contain(settlementId.ToString());
        body.Should().Contain("321000", "Admin phải thấy số tiền đang nói tới");
    }

    // ── Job phải báo cho ai đó khi nó giữ một khoản lại ─────────────────────

    [Fact]
    public async Task JobGiuKhoanLaiThiPhaiBaoAdmin()
    {
        // Trước MLACP-335, chỗ này chỉ đổi trạng thái rồi `continue` — không log, không báo ai. Mà
        // PendingReview lại không có đường ra, nên khoản tiền biến mất khỏi tầm nhìn của mọi người
        // trừ Owner, người vẫn thấy nó trong mục sắp nhận được.
        int settlementId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // Buổi diễn chạy 30 phút trên lịch 2 tiếng — tỉ lệ 0.25, dưới ngưỡng 0.70.
            var show = new LoungeShow
            {
                LoungeId = SeedHelper.LoungeId,
                Name = $"Show cat ngan {Guid.NewGuid():N}"[..20],
                Status = LoungeShowStatus.Ended,
                ScheduledStart = DateTimeOffset.UtcNow.AddDays(-20),
                ScheduledEnd = DateTimeOffset.UtcNow.AddDays(-20).AddHours(2),
                ActualStart = DateTimeOffset.UtcNow.AddDays(-20),
                ActualEnd = DateTimeOffset.UtcNow.AddDays(-20).AddMinutes(30),
                CreatedAt = DateTime.UtcNow
            };
            db.Add(show);
            await db.SaveChangesAsync();

            var payment = new Payment
            {
                OrderId = $"MLACP335J-{Guid.NewGuid():N}"[..30],
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
                ReleaseType = SettlementReleaseType.Final30,
                GrossAmount = 1_000_000m,
                PreRateApplied = 0.70m,
                PostRateApplied = 0.30m,
                NetAmount = 264_000m,
                Status = SettlementStatus.Scheduled,
                ScheduledAt = DateTimeOffset.UtcNow.AddDays(-1),   // da den han
                CreatedAt = DateTimeOffset.UtcNow
            };
            db.Add(settlement);
            await db.SaveChangesAsync();
            settlementId = settlement.Id;
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var job = scope.ServiceProvider
                .GetRequiredService<MusicLounge.Infrastructure.Jobs.SettlementReleaseJob>();
            await job.ExecuteAsync(new Hangfire.JobCancellationToken(false));
        }

        (await ReloadAsync(settlementId)).Status.Should().Be(SettlementStatus.PendingReview);

        using var verify = _factory.Services.CreateScope();
        var verifyDb = verify.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await verifyDb.Notifications.AnyAsync(n =>
            n.UserId == SeedHelper.AdminId
            && n.Type == NotificationType.SettlementPendingReview
            && n.ReferenceId == settlementId.ToString()))
            .Should().BeTrue(
                "giữ tiền lại rồi không báo ai thì không khác gì làm mất nó — không ai biết có " +
                "khoản đang chờ quyết");
    }

    // ── Hoàn tiền phải thu nhỏ cả khoản đang chờ duyệt ───────────────────────

    [Fact]
    public async Task DuyetHoanTienPhaiThuNhoCaKhoanDangChoDuyet()
    {
        // Trước MLACP-335, chỗ co giãn settlement chỉ lọc Status == Scheduled. Một tranche bị chốt
        // D16 giữ lại VẪN CHƯA chi trả đồng nào — nó chỉ đang đợi Admin quyết. Bỏ sót nó nghĩa là
        // nếu Admin sau đó bấm chi trả, phòng trà nhận đủ tiền cho cả phần đã hoàn cho khách.
        var (paymentId, settlementId) = await ParkedSettlementAsync(netAmount: 300_000m);

        int refundId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var payment = await db.Payments.FindAsync(paymentId);
            payment!.TransactionId = "TEST-TXN";
            payment.PaidAt = DateTimeOffset.UtcNow.AddDays(-1);

            // ProcessRefundRequest tra chu phong tra qua ve cua chinh giao dich do
            // (GetTicketShowOwnerIdAsync) — mot Payment khong ve thi no khong biet hoan cho ai.
            db.Add(new Ticket
            {
                Id = Guid.NewGuid(),
                BuyerId = SeedHelper.AudienceId,
                PriceId = SeedHelper.TicketPriceId,
                TierId = SeedHelper.TicketTierId,
                ShowId = SeedHelper.ShowId,
                PaymentId = paymentId,
                Status = TicketStatus.Confirmed,
                PurchaseChannel = PurchaseChannel.Online,
                CreatedAt = DateTimeOffset.UtcNow
            });

            var refund = new RefundRequest
            {
                PaymentId = paymentId,
                RequestedBy = SeedHelper.AudienceId,
                Reason = "Show bi cat ngan",
                AmountRequested = 500_000m,
                Status = RefundRequestStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };
            db.Add(refund);
            await db.SaveChangesAsync();
            refundId = refund.Id;
        }

        var res = await _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin")
            .PostAsJsonAsync($"/api/v1/admin/refund-requests/{refundId}/process",
                new { Decision = "Approved", ApprovedAmount = 500_000m });

        res.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Hoàn 500.000 trên tổng 1.000.000 là một nửa, nên tranche phải co lại một nửa.
        (await ReloadAsync(settlementId)).NetAmount
            .Should().Be(150_000m,
                "khoản đang chờ duyệt vẫn chưa chi đồng nào, nên nó phải co theo đúng tỉ lệ đã hoàn");
    }
}
