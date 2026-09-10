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
/// MLACP-347. Livestream đã lên sóng rồi bị cắt ngang — mất kết nối quá hạn chờ, bị Admin/kiểm
/// duyệt gỡ, hoặc chủ phòng trà kết thúc sớm — trước đây không dẫn tới gì cả: bốn đường kết thúc
/// chỉ đóng buổi diễn, <c>CancelTicket</c> từ chối vé của buổi đã kết thúc, và vé đã xem là
/// <c>Used</c> nên job MLACP-340 lẫn <c>ResolveComplaint</c> đều bỏ qua.
///
/// <para>Ngưỡng dùng lại <c>settlement_completion_threshold_pct</c> (seed 0.70) — cùng một định nghĩa
/// "không đạt" mà quyết toán đang dùng. Các bài dưới đây lấy lịch 100 phút để phần trăm đọc thẳng
/// ra từ số phút.</para>
/// </summary>
[Collection("Integration")]
public sealed class CutShortLivestreamRefundTests
{
    private const int ScheduledMinutes = 100;

    private readonly ApiFactory _factory;

    public CutShortLivestreamRefundTests(ApiFactory factory) => _factory = factory;

    private async Task<int> SeedShowAsync(
        LivestreamStatus streamStatus,
        int deliveredMinutes,
        int waitedMinutesAfterLoss = 5,
        LoungeShowFormat format = LoungeShowFormat.Online,
        bool declareEnd = true,
        int endedHoursAgo = 2)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Với Failed, EndedAt là lúc nền tảng thôi chờ — hình đã mất từ DisconnectedAt.
        var waited = streamStatus == LivestreamStatus.Failed ? waitedMinutesAfterLoss : 0;
        var endedAt = DateTimeOffset.UtcNow.AddHours(-endedHoursAgo);
        var startedAt = endedAt.AddMinutes(-(deliveredMinutes + waited));

        var show = new LoungeShow
        {
            LoungeId = SeedHelper.LoungeId,
            Name = $"CutShort {Guid.NewGuid():N}"[..20],
            Format = format,
            Status = LoungeShowStatus.Ended,
            ScheduledStart = startedAt,
            ScheduledEnd = declareEnd ? startedAt.AddMinutes(ScheduledMinutes) : null,
            ActualStart = startedAt,
            ActualEnd = endedAt,
            RatingOpenUntil = DateTimeOffset.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow
        };
        db.Add(show);
        await db.SaveChangesAsync();

        db.Add(new Livestream
        {
            LoungeShowId = show.Id,
            Status = streamStatus,
            StartedAt = startedAt,
            DisconnectedAt = streamStatus == LivestreamStatus.Failed
                ? startedAt.AddMinutes(deliveredMinutes)
                : null,
            EndedAt = endedAt,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        return show.Id;
    }

    private async Task<(Guid TicketId, int PaymentId)> AddTicketAsync(
        int showId,
        TicketStatus status,
        AccessType accessType = AccessType.Livestream,
        int? existingPaymentId = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var tier = new TicketTier
        {
            LoungeShowId = showId,
            Name = accessType.ToString(),
            AccessType = accessType,
            CreatedAt = DateTime.UtcNow
        };
        db.Add(tier);
        await db.SaveChangesAsync();

        var price = new TicketPrice
        {
            TierId = tier.Id,
            Name = "Đợt 1",
            Price = 300_000m,
            PurchaseChannel = PurchaseChannel.Online,
            SaleStart = DateTimeOffset.UtcNow.AddDays(-30)
        };
        db.Add(price);
        await db.SaveChangesAsync();

        var paymentId = existingPaymentId;
        if (paymentId is null)
        {
            var payment = new Payment
            {
                OrderId = $"MLACP347-{Guid.NewGuid():N}"[..30],
                PayerId = SeedHelper.AudienceId,
                GrossAmount = 300_000m,
                NetAmount = 300_000m,
                Status = PaymentStatus.Confirmed,
                ReferenceType = "TicketHold",
                ReferenceId = "0",
                TransactionId = $"S{Guid.NewGuid():N}"[..16],
                PaidAt = DateTimeOffset.UtcNow.AddDays(-2),
                CreatedAt = DateTimeOffset.UtcNow.AddDays(-2)
            };
            db.Add(payment);
            await db.SaveChangesAsync();
            paymentId = payment.Id;
        }

        var ticket = new Ticket
        {
            Id = Guid.NewGuid(),
            BuyerId = SeedHelper.AudienceId,
            PriceId = price.Id,
            TierId = tier.Id,
            ShowId = showId,
            PaymentId = paymentId,
            Status = status,
            PurchaseChannel = PurchaseChannel.Online,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-2)
        };
        db.Add(ticket);
        await db.SaveChangesAsync();

        return (ticket.Id, paymentId.Value);
    }

    private async Task RunJobAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var job = scope.ServiceProvider.GetRequiredService<RefundUndeliveredLivestreamTicketsJob>();
        await job.ExecuteAsync(new JobCancellationToken(false));
    }

    private async Task<TicketStatus> TicketStatusAsync(Guid ticketId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return (await db.Tickets.SingleAsync(t => t.Id == ticketId)).Status;
    }

    private async Task<List<RefundRequest>> RefundsAsync(int paymentId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.RefundRequests.Where(r => r.PaymentId == paymentId).ToListAsync();
    }

    private async Task<int> CutShortNoticesAsync(int userId, int showId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Notifications.CountAsync(n =>
            n.UserId == userId
            && n.Type == NotificationType.LivestreamCutShort
            && n.ReferenceType == "show"
            && n.ReferenceId == showId.ToString());
    }

    // ── Dưới ngưỡng thì hoàn, bất kể vì sao bị cắt ──────────────────────────

    [Theory]
    [InlineData(LivestreamStatus.Ended)]      // chủ phòng trà bấm kết thúc sớm / webhook Mux idle
    [InlineData(LivestreamStatus.Terminated)] // Admin hoặc kiểm duyệt gỡ
    [InlineData(LivestreamStatus.Failed)]     // mất kết nối quá hạn chờ
    public async Task DuoiNguongThiMoiVeLivestreamDuocHoan100(LivestreamStatus streamStatus)
    {
        var showId = await SeedShowAsync(streamStatus, deliveredMinutes: 40);
        var neverWatched = await AddTicketAsync(showId, TicketStatus.Confirmed);
        var watched = await AddTicketAsync(showId, TicketStatus.Used);

        await RunJobAsync();

        (await TicketStatusAsync(neverWatched.TicketId)).Should().Be(TicketStatus.Cancelled);
        (await TicketStatusAsync(watched.TicketId)).Should().Be(TicketStatus.Refunded,
            "người đã xem được hoàn nhưng vẫn phải giữ quyền đánh giá — Cancelled sẽ tước quyền đó");

        foreach (var paymentId in new[] { neverWatched.PaymentId, watched.PaymentId })
        {
            var refunds = await RefundsAsync(paymentId);
            refunds.Should().ContainSingle();
            refunds[0].RefundPercentage.Should().Be(100m);
            refunds[0].AmountRequested.Should().Be(300_000m);
            refunds[0].Status.Should().Be(RefundRequestStatus.Pending);
        }
    }

    [Fact]
    public async Task NguoiDaXemVaDuocHoanVanDanhGiaDuoc()
    {
        var showId = await SeedShowAsync(LivestreamStatus.Failed, deliveredMinutes: 30);
        await AddTicketAsync(showId, TicketStatus.Used);

        await RunJobAsync();

        var res = await _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience")
            .PostAsJsonAsync($"/api/v1/lounge-shows/{showId}/rate",
                new { Score = 1, Comment = "Mất sóng giữa chừng" });

        res.StatusCode.Should().Be(HttpStatusCode.NoContent,
            "chính những người này là nhân chứng của buổi diễn hỏng — chặn họ thì buổi tệ nhất của " +
            "phòng trà lại là buổi không có đánh giá nào");
    }

    [Fact]
    public async Task DoDenLucMatHinhChuKhongPhaiLucNenTangThoiCho()
    {
        // Mất hình ở phút 68 (68%), nền tảng chờ thêm 5 phút rồi mới đánh dấu Failed (73%).
        // Khoảng chờ không phát gì — tính nó vào là cho phòng trà "giao" 5 phút không có hình.
        var showId = await SeedShowAsync(
            LivestreamStatus.Failed, deliveredMinutes: 68, waitedMinutesAfterLoss: 5);
        var ticket = await AddTicketAsync(showId, TicketStatus.Used);

        await RunJobAsync();

        (await RefundsAsync(ticket.PaymentId)).Should().ContainSingle(
            "68% là dưới ngưỡng 70%; chỉ khi đo tới lúc nền tảng thôi chờ mới thành 73%");
    }

    [Fact]
    public async Task ChuPhongTraDuocBao()
    {
        var showId = await SeedShowAsync(LivestreamStatus.Failed, deliveredMinutes: 40);
        await AddTicketAsync(showId, TicketStatus.Used);

        await RunJobAsync();

        (await CutShortNoticesAsync(SeedHelper.OwnerId, showId)).Should().Be(1,
            "doanh thu của họ vừa bị trừ — không được để họ tự phát hiện qua quyết toán");
        (await CutShortNoticesAsync(SeedHelper.AudienceId, showId)).Should().Be(1);
    }

    // ── Chống trùng theo từng vé ────────────────────────────────────────────

    [Fact]
    public async Task ChayNhieuLanCungChiMotYeuCauMoiVe()
    {
        var showId = await SeedShowAsync(LivestreamStatus.Terminated, deliveredMinutes: 40);
        var ticket = await AddTicketAsync(showId, TicketStatus.Used);

        await RunJobAsync();
        await RunJobAsync();

        (await RefundsAsync(ticket.PaymentId)).Should().ContainSingle();
        (await CutShortNoticesAsync(SeedHelper.AudienceId, showId)).Should().Be(1);
    }

    [Fact]
    public async Task MotThanhToanNhieuVeThiVeConLaiVanDuocHoan()
    {
        // Mua 2 vé trong một thanh toán, tự huỷ 1 vé trước giờ diễn (còn chờ duyệt), xem bằng vé
        // kia rồi mất sóng. Chống trùng theo THANH TOÁN sẽ thấy "đã có yêu cầu hoàn" và bỏ qua.
        var showId = await SeedShowAsync(LivestreamStatus.Failed, deliveredMinutes: 40);
        var cancelledEarlier = await AddTicketAsync(showId, TicketStatus.Cancelled);
        var watched = await AddTicketAsync(
            showId, TicketStatus.Used, existingPaymentId: cancelledEarlier.PaymentId);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Add(new RefundRequest
            {
                PaymentId = cancelledEarlier.PaymentId,
                RequestedBy = SeedHelper.AudienceId,
                Reason = "Audience yêu cầu hủy vé",
                AmountRequested = 240_000m,
                RefundPercentage = 80m,
                Status = RefundRequestStatus.Pending,
                CreatedAt = DateTime.UtcNow.AddDays(-1)
            });
            await db.SaveChangesAsync();
        }

        await RunJobAsync();

        (await TicketStatusAsync(watched.TicketId)).Should().Be(TicketStatus.Refunded);
        (await RefundsAsync(watched.PaymentId)).Should().HaveCount(2,
            "vé huỷ từ trước có yêu cầu riêng của nó; vé đã xem cũng phải có yêu cầu của mình");
    }

    // ── Không đủ căn cứ thì không tự động ───────────────────────────────────

    [Fact]
    public async Task DatNguongVaKetThucBinhThuongThiKhongHoanKhongBao()
    {
        var showId = await SeedShowAsync(LivestreamStatus.Ended, deliveredMinutes: 90);
        var ticket = await AddTicketAsync(showId, TicketStatus.Used);

        await RunJobAsync();

        (await TicketStatusAsync(ticket.TicketId)).Should().Be(TicketStatus.Used);
        (await RefundsAsync(ticket.PaymentId)).Should().BeEmpty();
        (await CutShortNoticesAsync(SeedHelper.AudienceId, showId)).Should().Be(0);
    }

    [Fact]
    public async Task BiCatBatThuongNhungDatNguongThiChiBaoMotLan()
    {
        var showId = await SeedShowAsync(LivestreamStatus.Failed, deliveredMinutes: 85);
        var ticket = await AddTicketAsync(showId, TicketStatus.Used);

        await RunJobAsync();
        await RunJobAsync();

        (await RefundsAsync(ticket.PaymentId)).Should().BeEmpty();
        (await TicketStatusAsync(ticket.TicketId)).Should().Be(TicketStatus.Used);
        (await CutShortNoticesAsync(SeedHelper.AudienceId, showId)).Should().Be(1,
            "người xem cần biết vì sao hình mất — và chỉ một lần");
    }

    [Fact]
    public async Task KhongKhaiBaoGioKetThucThiKhongHoan()
    {
        // Không có ScheduledEnd thì thời lượng "đã bán" rơi về mặc định 4 giờ: 90 phút diễn trọn
        // sẽ bị tính là giao 37%. Đó là phạt phòng trà vì một ô không điền.
        var showId = await SeedShowAsync(
            LivestreamStatus.Failed, deliveredMinutes: 90, declareEnd: false);
        var ticket = await AddTicketAsync(showId, TicketStatus.Used);

        await RunJobAsync();

        (await RefundsAsync(ticket.PaymentId)).Should().BeEmpty();
        (await CutShortNoticesAsync(SeedHelper.AudienceId, showId)).Should().Be(1,
            "không kết luận được về thời lượng, nhưng stream vẫn là mất tín hiệu — người xem vẫn phải được báo");
    }

    [Fact]
    public async Task ShowHybridThiVeOfflineKhongBiDongToi()
    {
        var showId = await SeedShowAsync(
            LivestreamStatus.Failed, deliveredMinutes: 40, format: LoungeShowFormat.Hybrid);
        var physical = await AddTicketAsync(showId, TicketStatus.Used, AccessType.Physical);
        var online = await AddTicketAsync(showId, TicketStatus.Used);

        await RunJobAsync();

        (await RefundsAsync(online.PaymentId)).Should().ContainSingle();
        (await TicketStatusAsync(physical.TicketId)).Should().Be(TicketStatus.Used,
            "phòng thật có thể vẫn diễn tiếp — bằng chứng về stream không nói được gì về phòng");
        (await RefundsAsync(physical.PaymentId)).Should().BeEmpty();
    }

    [Fact]
    public async Task StreamKetThucQuaLauThiNhanhTuDongKhongXet()
    {
        var showId = await SeedShowAsync(
            LivestreamStatus.Failed, deliveredMinutes: 40, endedHoursAgo: 24 * 20);
        var ticket = await AddTicketAsync(showId, TicketStatus.Used);

        await RunJobAsync();

        (await RefundsAsync(ticket.PaymentId)).Should().BeEmpty(
            "quá mốc giải ngân tranche cuối thì nhánh tự động không còn gì để giữ lại");
    }
}
