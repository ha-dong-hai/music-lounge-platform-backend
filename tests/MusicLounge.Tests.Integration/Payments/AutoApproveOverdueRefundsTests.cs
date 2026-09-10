using System.Net;
using System.Net.Http.Json;
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
/// MLACP-348. Yêu cầu hoàn quá <c>refund_sla_hours</c> (72h) trước đây chỉ được báo cho Admin — nếu
/// Admin không xử lý, người mua chờ vô hạn, trong khi kháng cáo quá hạn của phòng trà thì đã được tự
/// chấp nhận. Nay quá thêm <c>refund_auto_approve_grace_hours</c> (mặc định 24h) thì hệ thống tự duyệt
/// qua đúng <c>ProcessRefundRequestCommand</c> mà Admin dùng.
/// </summary>
[Collection("Integration")]
public sealed class AutoApproveOverdueRefundsTests
{
    private readonly ApiFactory _factory;

    public AutoApproveOverdueRefundsTests(ApiFactory factory) => _factory = factory;

    private async Task<int> SeedPendingRefundAsync(
        int createdHoursAgo,
        PaymentMethod method = PaymentMethod.Gateway,
        int paidDaysAgo = 5)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var paidAt = DateTimeOffset.UtcNow.AddDays(-paidDaysAgo);
        var isGateway = method == PaymentMethod.Gateway;

        var payment = new Payment
        {
            OrderId = $"MLACP348-{Guid.NewGuid():N}"[..30],
            PayerId = SeedHelper.AudienceId,
            // Thanh toán cổng đã xác nhận LUÔN có mã giao dịch; tiền mặt tại quầy thì không bao giờ có.
            TransactionId = isGateway ? $"A{Guid.NewGuid():N}"[..16] : null,
            GrossAmount = 200_000m,
            PlatformFee = isGateway ? 10_000m : 0m,
            TaxWithheld = isGateway ? 10_000m : 0m,
            NetAmount = isGateway ? 180_000m : 200_000m,
            Method = method,
            Status = PaymentStatus.Confirmed,
            ReferenceType = isGateway ? "TicketHold" : "WalkIn",
            ReferenceId = "0",
            PaidAt = paidAt,
            CreatedAt = paidAt
        };
        db.Add(payment);
        await db.SaveChangesAsync();

        db.Add(new Ticket
        {
            Id = Guid.NewGuid(),
            BuyerId = SeedHelper.AudienceId,
            PriceId = SeedHelper.TicketPriceId,
            TierId = SeedHelper.TicketTierId,
            ShowId = SeedHelper.ShowId,
            PaymentId = payment.Id,
            Status = TicketStatus.Cancelled,
            PurchaseChannel = isGateway ? PurchaseChannel.Online : PurchaseChannel.Offline,
            CreatedAt = paidAt
        });

        var refund = new RefundRequest
        {
            PaymentId = payment.Id,
            RequestedBy = SeedHelper.AudienceId,
            Reason = "Audience yêu cầu hủy vé",
            AmountRequested = 200_000m,
            RefundPercentage = 100m,
            Status = RefundRequestStatus.Pending
        };
        db.Add(refund);
        await db.SaveChangesAsync();

        // CreatedAt được SaveChanges đóng dấu, nên phải lùi lại sau đó.
        refund.CreatedAt = DateTime.UtcNow.AddHours(-createdHoursAgo);
        await db.SaveChangesAsync();

        return refund.Id;
    }

    private async Task RunAutoApproveAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var job = scope.ServiceProvider.GetRequiredService<AutoApproveOverdueRefundsJob>();
        await job.ExecuteAsync(new JobCancellationToken(false));
    }

    private async Task RunSlaAlertAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var job = scope.ServiceProvider.GetRequiredService<RefundSlaBreachAlertJob>();
        await job.ExecuteAsync(new JobCancellationToken(false));
    }

    private async Task<RefundRequest> RefundAsync(int refundId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.RefundRequests.AsNoTracking().SingleAsync(r => r.Id == refundId);
    }

    private async Task<int> CountAsync(int userId, NotificationType type, string referenceType, int refundId,
        string? title = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Notifications.CountAsync(n =>
            n.UserId == userId && n.Type == type
            && n.ReferenceType == referenceType && n.ReferenceId == refundId.ToString()
            && (title == null || n.Title == title));
    }

    // ── Quá SLA và hết ân hạn thì tự duyệt ──────────────────────────────────

    [Fact]
    public async Task QuaHanVaHetAnHanThiTuDuyetQuaDungDuongCuaAdmin()
    {
        var refundId = await SeedPendingRefundAsync(createdHoursAgo: 97); // 72h + 24h đã qua

        await RunAutoApproveAsync();

        var refund = await RefundAsync(refundId);
        refund.Status.Should().Be(RefundRequestStatus.Approved);
        refund.AmountApproved.Should().Be(200_000m, "số tiền là số hệ thống đã tính khi tạo yêu cầu");
        refund.ProcessedBy.Should().BeNull("không có Admin nào duyệt — null nghĩa là hệ thống");
        refund.ResolutionNote.Should().Contain("Tự động duyệt");

        (await CountAsync(SeedHelper.AudienceId, NotificationType.RefundUpdate, "refund", refundId))
            .Should().Be(1, "người mua được báo như khi Admin duyệt tay — cùng một handler");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.LedgerEntries.AnyAsync(e => e.ReferenceType == "refund" && e.ReferenceId == refundId.ToString()))
            .Should().BeTrue("bút toán đảo phải được ghi — không phải một bản sao logic thiếu bước");
    }

    [Fact]
    public async Task ChuaHetAnHanThiChuaDuyet()
    {
        // Đã quá SLA 72h nhưng chưa quá thêm 24h ân hạn: Admin vẫn còn thời gian để từ chối.
        var refundId = await SeedPendingRefundAsync(createdHoursAgo: 90);

        await RunAutoApproveAsync();

        (await RefundAsync(refundId)).Status.Should().Be(RefundRequestStatus.Pending);
    }

    [Fact]
    public async Task AdminDuocBaoHeThongDaThayHoQuyet()
    {
        var refundId = await SeedPendingRefundAsync(createdHoursAgo: 100);

        await RunAutoApproveAsync();

        (await CountAsync(SeedHelper.AdminId, NotificationType.RefundSlaBreached, "refund_request", refundId,
                "Đã tự động duyệt hoàn tiền"))
            .Should().Be(1);
    }

    [Fact]
    public async Task VeTienMatThiPhongTraDuocBaoPhaiTraKhach()
    {
        var refundId = await SeedPendingRefundAsync(createdHoursAgo: 100, method: PaymentMethod.Cash);

        await RunAutoApproveAsync();

        (await RefundAsync(refundId)).Status.Should().Be(RefundRequestStatus.Approved);
        (await CountAsync(SeedHelper.OwnerId, NotificationType.RefundOwedByVenue, "refund", refundId))
            .Should().Be(1, "nền tảng chưa từng giữ khoản tiền mặt — phòng trà mới là người phải trả");
    }

    // ── Một yêu cầu hỏng không được làm bẩn yêu cầu khác ────────────────────

    [Fact]
    public async Task YeuCauBiTuChoiKhongDeLaiDauVetKhiYeuCauSauDuocDuyet()
    {
        // A: đã quá 90 ngày VNPay nhận lệnh hoàn — handler từ chối, SAU KHI đã ghi ProcessedBy/
        // ResolvedAt/ResolutionNote lên thực thể. B: bình thường, xử lý ngay sau A.
        var refundA = await SeedPendingRefundAsync(createdHoursAgo: 100, paidDaysAgo: 100);
        var refundB = await SeedPendingRefundAsync(createdHoursAgo: 100);

        await RunAutoApproveAsync();

        var a = await RefundAsync(refundA);
        a.Status.Should().Be(RefundRequestStatus.Pending);
        a.ResolvedAt.Should().BeNull(
            "dùng chung DbContext thì lần lưu của B sẽ ghi luôn các trường A đã sửa dở — một yêu cầu " +
            "vẫn chờ lại mang dấu như đã được xử lý");
        a.ResolutionNote.Should().BeNull();

        (await RefundAsync(refundB)).Status.Should().Be(RefundRequestStatus.Approved);
    }

    // ── Đường Admin duyệt tay không đổi ─────────────────────────────────────

    [Fact]
    public async Task AdminDuyetTayVanGhiDungNguoiDuyet()
    {
        var refundId = await SeedPendingRefundAsync(createdHoursAgo: 1);

        var res = await _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin")
            .PostAsJsonAsync($"/api/v1/admin/refund-requests/{refundId}/process",
                new { Decision = "Approved" });

        res.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await RefundAsync(refundId)).ProcessedBy.Should().Be(SeedHelper.AdminId);
    }

    // ── Cảnh báo SLA: một lần, nói rõ mốc tự duyệt ──────────────────────────

    [Fact]
    public async Task CanhBaoQuaHanChiGuiMotLanVaNoiRoMocTuDuyet()
    {
        var refundId = await SeedPendingRefundAsync(createdHoursAgo: 80);

        await RunSlaAlertAsync();
        await RunSlaAlertAsync();

        (await CountAsync(SeedHelper.AdminId, NotificationType.RefundSlaBreached, "refund_request", refundId,
                "Quá hạn xử lý hoàn tiền"))
            .Should().Be(1, "trước đây mỗi lần chạy (mỗi giờ) lại gửi đúng cảnh báo đó thêm một lần");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var body = await db.Notifications
            .Where(n => n.UserId == SeedHelper.AdminId && n.Title == "Quá hạn xử lý hoàn tiền"
                        && n.ReferenceId == refundId.ToString())
            .Select(n => n.Body)
            .SingleAsync();
        body.Should().Contain("tự duyệt", "Admin phải biết im lặng sẽ dẫn tới đâu");
    }
}
