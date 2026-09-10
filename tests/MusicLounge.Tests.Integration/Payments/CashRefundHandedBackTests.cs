using System.Net;
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
/// MLACP-345. MLACP-337 xử lý đúng việc duyệt hoàn cho vé bán tại quầy: nền tảng chưa bao giờ giữ
/// khoản tiền mặt đó, nên hệ thống báo phòng trà rằng chính họ phải trả cho khách
/// (<c>RefundOwedByVenue</c>).
///
/// <para>Nhưng sau lời nhắc đó <b>không ai theo dõi tiếp</b>: <c>RefundOwedByVenue</c> chỉ được ghi
/// ở đúng một chỗ, và <c>RefundSlaBreachAlertJob</c> chỉ quét yêu cầu <c>Pending</c> — một yêu cầu
/// đã <c>Approved</c> coi như đã xong. Đúng với vé online (VNPay đã chuyển tiền thật), sai với vé
/// tiền mặt (mới chỉ có một lời nhắc). Lần thứ năm của lớp lỗi "hứa mà không có cơ chế".</para>
/// </summary>
[Collection("Integration")]
public sealed class CashRefundHandedBackTests
{
    private readonly ApiFactory _factory;

    public CashRefundHandedBackTests(ApiFactory factory) => _factory = factory;

    private async Task<int> ApprovedRefundAsync(
        PaymentMethod method = PaymentMethod.Cash,
        RefundRequestStatus status = RefundRequestStatus.Approved,
        int resolvedHoursAgo = 1,
        bool handedBack = false)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var payment = new Payment
        {
            OrderId = $"MLACP345-{Guid.NewGuid():N}"[..30],
            PayerId = SeedHelper.AudienceId,
            GrossAmount = 180_000m,
            NetAmount = 180_000m,
            Method = method,
            Status = PaymentStatus.Confirmed,
            ReferenceType = method == PaymentMethod.Cash ? "WalkIn" : "TicketHold",
            ReferenceId = "0",
            TransactionId = method == PaymentMethod.Gateway ? $"C{Guid.NewGuid():N}"[..16] : null,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-3)
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
            PurchaseChannel = method == PaymentMethod.Cash ? PurchaseChannel.Offline : PurchaseChannel.Online,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-3)
        });

        var refund = new RefundRequest
        {
            PaymentId = payment.Id,
            RequestedBy = SeedHelper.AudienceId,
            Reason = "Buoi dien khong dien ra",
            AmountRequested = 180_000m,
            AmountApproved = status == RefundRequestStatus.Approved ? 180_000m : null,
            RefundPercentage = 100m,
            Status = status,
            ResolvedAt = status == RefundRequestStatus.Pending
                ? null
                : DateTimeOffset.UtcNow.AddHours(-resolvedHoursAgo),
            CashHandedBackAt = handedBack ? DateTimeOffset.UtcNow.AddHours(-1) : null,
            CreatedAt = DateTime.UtcNow.AddHours(-resolvedHoursAgo - 1)
        };
        db.Add(refund);
        await db.SaveChangesAsync();

        return refund.Id;
    }

    private Task<HttpResponseMessage> ConfirmAsync(int refundId, int userId, string role)
        => _factory.CreateAuthenticatedClient(userId, role)
            .PostAsync($"/api/v1/tickets/refund-requests/{refundId}/cash-handed-back", null);

    private async Task<int> CountAsync(int userId, NotificationType type, string referenceType, int refundId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Notifications.CountAsync(n =>
            n.UserId == userId && n.Type == type
            && n.ReferenceType == referenceType && n.ReferenceId == refundId.ToString());
    }

    private async Task RunSlaJobAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var job = scope.ServiceProvider.GetRequiredService<RefundSlaBreachAlertJob>();
        await job.ExecuteAsync(new JobCancellationToken(false));
    }

    // ── Phòng trà xác nhận đã trả ───────────────────────────────────────────

    [Fact]
    public async Task ChuPhongTraXacNhanDaTraThiNguoiMuaDuocBao()
    {
        var refundId = await ApprovedRefundAsync();

        var res = await ConfirmAsync(refundId, SeedHelper.OwnerId, "Owner");

        res.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.RefundRequests.FindAsync(refundId))!.CashHandedBackAt.Should().NotBeNull();

        (await CountAsync(SeedHelper.AudienceId, NotificationType.RefundUpdate, "refund", refundId))
            .Should().Be(1, "khách phải biết phòng trà đã xác nhận trả — kèm lối khiếu nại nếu chưa nhận");
    }

    [Fact]
    public async Task XacNhanLanThuHaiBiTuChoi()
    {
        var refundId = await ApprovedRefundAsync();

        (await ConfirmAsync(refundId, SeedHelper.OwnerId, "Owner")).StatusCode
            .Should().Be(HttpStatusCode.NoContent);

        (await ConfirmAsync(refundId, SeedHelper.OwnerId, "Owner")).StatusCode
            .Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task ChuPhongTraKhacKhongXacNhanDuoc()
    {
        var refundId = await ApprovedRefundAsync();

        var res = await ConfirmAsync(refundId, SeedHelper.OtherOwnerId, "Owner");

        res.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "chỉ nhân viên hoặc chủ của đúng phòng trà mới là người trả tiền mặt tại quầy");
    }

    [Fact]
    public async Task VeMuaOnlineKhongCanPhongTraXacNhan()
    {
        var refundId = await ApprovedRefundAsync(method: PaymentMethod.Gateway);

        var res = await ConfirmAsync(refundId, SeedHelper.OwnerId, "Owner");

        res.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "vé online được hoàn qua cổng thanh toán — phòng trà không cầm khoản đó");
    }

    [Fact]
    public async Task YeuCauChuaDuyetThiChuaXacNhanDuoc()
    {
        var refundId = await ApprovedRefundAsync(status: RefundRequestStatus.Pending);

        var res = await ConfirmAsync(refundId, SeedHelper.OwnerId, "Owner");

        res.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    // ── Quá hạn mà chưa trả thì phải có người biết ──────────────────────────

    [Fact]
    public async Task QuaHanMaChuaTraThiNhacChuPhongTraVaBaoAdmin()
    {
        // Duyệt từ 80 tiếng trước, cam kết 72 tiếng.
        var refundId = await ApprovedRefundAsync(resolvedHoursAgo: 80);

        await RunSlaJobAsync();

        (await CountAsync(SeedHelper.OwnerId, NotificationType.RefundOwedByVenue, "cash_refund", refundId))
            .Should().Be(1, "phòng trà là người đang nợ khách");
        (await CountAsync(SeedHelper.AdminId, NotificationType.RefundSlaBreached, "cash_refund", refundId))
            .Should().Be(1, "Admin là người có thể can thiệp");
    }

    [Fact]
    public async Task ChayNhieuLanCungChiNhacMotLan()
    {
        var refundId = await ApprovedRefundAsync(resolvedHoursAgo: 80);

        await RunSlaJobAsync();
        await RunSlaJobAsync();

        (await CountAsync(SeedHelper.OwnerId, NotificationType.RefundOwedByVenue, "cash_refund", refundId))
            .Should().Be(1);
    }

    [Fact]
    public async Task DaXacNhanTraRoiThiKhongNhac()
    {
        var refundId = await ApprovedRefundAsync(resolvedHoursAgo: 80, handedBack: true);

        await RunSlaJobAsync();

        (await CountAsync(SeedHelper.OwnerId, NotificationType.RefundOwedByVenue, "cash_refund", refundId))
            .Should().Be(0);
    }

    [Fact]
    public async Task VeOnlineDaDuyetThiKhongNamTrongDienNay()
    {
        // VNPay đã chuyển tiền thật khi duyệt — không có gì để phòng trà trả.
        var refundId = await ApprovedRefundAsync(method: PaymentMethod.Gateway, resolvedHoursAgo: 80);

        await RunSlaJobAsync();

        (await CountAsync(SeedHelper.AdminId, NotificationType.RefundSlaBreached, "cash_refund", refundId))
            .Should().Be(0);
    }
}
