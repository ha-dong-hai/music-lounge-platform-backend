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
/// MLACP-340. Với vé livestream, nền tảng <b>chính là kênh giao hàng</b>. Nếu
/// <c>Livestream.Status</c> chưa bao giờ là <c>Live</c> thì không một người mua nào xem được gì —
/// và đó là bằng chứng nằm trong hồ sơ của chính nền tảng, không phải lời cáo buộc nhắm vào phòng
/// trà, không phải chuyện phải đi hỏi ai.
///
/// <para>Sau MLACP-336/338 người mua <b>tự</b> đòi lại được 100%, nhưng họ phải tự nhận ra. Với
/// trường hợp mà nền tảng đã tự chứng minh được là không giao được hàng, bắt khách phải tự đi đòi
/// là sai — Ticketmaster với sự kiện bị huỷ là hoàn tự động, khách không phải làm gì.</para>
///
/// <para><b>Chỉ vé livestream.</b> Với show <c>Hybrid</c>, người mua vé Physical vẫn đi đường xác
/// minh riêng — phòng trà có thể đã chạy phòng thật, và bằng chứng về stream không nói được gì về
/// phòng.</para>
/// </summary>
[Collection("Integration")]
public sealed class UndeliveredLivestreamAutoRefundTests
{
    private readonly ApiFactory _factory;

    public UndeliveredLivestreamAutoRefundTests(ApiFactory factory) => _factory = factory;

    private sealed record Seeded(int ShowId, Guid LivestreamTicketId, Guid? PhysicalTicketId);

    private async Task<Seeded> SeedAsync(
        LoungeShowFormat format,
        DateTimeOffset? actualStart,
        int endedHoursAgo = 12,
        bool withLivestream = true)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var end = DateTimeOffset.UtcNow.AddHours(-endedHoursAgo);
        var start = end.AddHours(-2);

        var show = new LoungeShow
        {
            LoungeId = SeedHelper.LoungeId,
            Name = $"Show {Guid.NewGuid():N}"[..18],
            Format = format,
            Status = LoungeShowStatus.Ended,
            ScheduledStart = start,
            ScheduledEnd = end,
            ActualStart = actualStart,
            ActualEnd = end,
            CreatedAt = DateTime.UtcNow
        };
        db.Add(show);
        await db.SaveChangesAsync();

        if (withLivestream)
        {
            db.Add(new Livestream
            {
                LoungeShowId = show.Id,
                Status = actualStart is null ? LivestreamStatus.Scheduled : LivestreamStatus.Ended,
                CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        var livestreamTicketId = await AddTicketAsync(db, show.Id, AccessType.Livestream);
        Guid? physicalTicketId = format == LoungeShowFormat.Hybrid
            ? await AddTicketAsync(db, show.Id, AccessType.Physical)
            : null;

        return new Seeded(show.Id, livestreamTicketId, physicalTicketId);
    }

    private static async Task<Guid> AddTicketAsync(
        ApplicationDbContext db, int showId, AccessType accessType)
    {
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

        var payment = new Payment
        {
            OrderId = $"MLACP340-{Guid.NewGuid():N}"[..30],
            PayerId = SeedHelper.AudienceId,
            GrossAmount = 300_000m,
            NetAmount = 300_000m,
            Status = PaymentStatus.Confirmed,
            ReferenceType = "TicketHold",
            ReferenceId = "0",
            TransactionId = $"L{Guid.NewGuid():N}"[..16],
            PaidAt = DateTimeOffset.UtcNow.AddDays(-2),
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-2)
        };
        db.Add(payment);
        await db.SaveChangesAsync();

        var ticket = new Ticket
        {
            Id = Guid.NewGuid(),
            BuyerId = SeedHelper.AudienceId,
            PriceId = price.Id,
            TierId = tier.Id,
            ShowId = showId,
            PaymentId = payment.Id,
            Status = TicketStatus.Confirmed,
            PurchaseChannel = PurchaseChannel.Online,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-2)
        };
        db.Add(ticket);
        await db.SaveChangesAsync();

        return ticket.Id;
    }

    private async Task RunJobAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var job = scope.ServiceProvider.GetRequiredService<RefundUndeliveredLivestreamTicketsJob>();
        await job.ExecuteAsync(new JobCancellationToken(false));
    }

    private async Task<(TicketStatus Status, bool HasRefund)> TicketStateAsync(Guid ticketId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var ticket = await db.Tickets.SingleAsync(t => t.Id == ticketId);
        var hasRefund = ticket.PaymentId.HasValue
            && await db.RefundRequests.AnyAsync(r => r.PaymentId == ticket.PaymentId.Value);
        return (ticket.Status, hasRefund);
    }

    // ── Kết luận được thì hoàn tự động ──────────────────────────────────────

    [Fact]
    public async Task StreamChuaTungLenSongThiVeLivestreamDuocHoanTuDong()
    {
        var seeded = await SeedAsync(LoungeShowFormat.Online, actualStart: null);

        await RunJobAsync();

        var (status, hasRefund) = await TicketStateAsync(seeded.LivestreamTicketId);
        status.Should().Be(TicketStatus.Cancelled);
        hasRefund.Should().BeTrue(
            "nền tảng chính là kênh giao hàng — nó đã tự chứng minh được là không giao được gì, " +
            "nên bắt khách tự đi đòi là sai");
    }

    [Fact]
    public async Task NguoiMuaPhaiDuocBaoChuKhongPhaiTuNhanRa()
    {
        var seeded = await SeedAsync(LoungeShowFormat.Online, actualStart: null);

        await RunJobAsync();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.Notifications.AnyAsync(n =>
            n.UserId == SeedHelper.AudienceId
            && n.ReferenceType == "show"
            && n.ReferenceId == seeded.ShowId.ToString()))
            .Should().BeTrue();
    }

    [Fact]
    public async Task ChayNhieuLanCungChiTaoMotYeuCauHoan()
    {
        var seeded = await SeedAsync(LoungeShowFormat.Online, actualStart: null);

        await RunJobAsync();
        await RunJobAsync();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var paymentId = await db.Tickets.Where(t => t.Id == seeded.LivestreamTicketId)
            .Select(t => t.PaymentId!.Value).SingleAsync();

        (await db.RefundRequests.CountAsync(r => r.PaymentId == paymentId)).Should().Be(1,
            "vé đã sang Cancelled ở lần chạy đầu, và đó cũng là chốt chống tạo yêu cầu trùng");
    }

    // ── Show Hybrid: hai vế phải được đối xử khác nhau ──────────────────────

    [Fact]
    public async Task ShowHybridThiChiHoanVeLivestreamChuKhongDongToiVeOffline()
    {
        var seeded = await SeedAsync(LoungeShowFormat.Hybrid, actualStart: null);

        await RunJobAsync();

        (await TicketStateAsync(seeded.LivestreamTicketId)).HasRefund.Should().BeTrue();

        var physical = await TicketStateAsync(seeded.PhysicalTicketId!.Value);
        physical.Status.Should().Be(TicketStatus.Confirmed,
            "phòng trà có thể đã chạy phòng thật — bằng chứng về stream không nói được gì về phòng");
        physical.HasRefund.Should().BeFalse();
    }

    // ── Không kết luận được thì không tự động ───────────────────────────────

    [Fact]
    public async Task StreamDaLenSongThiKhongHoanGiCa()
    {
        var start = DateTimeOffset.UtcNow.AddHours(-14);
        var seeded = await SeedAsync(LoungeShowFormat.Online, actualStart: start);

        await RunJobAsync();

        var (status, hasRefund) = await TicketStateAsync(seeded.LivestreamTicketId);
        status.Should().Be(TicketStatus.Confirmed);
        hasRefund.Should().BeFalse();
    }

    [Fact]
    public async Task ChuaQuaBienAnToanThiChuaHoan()
    {
        // StartLivestream không chặn theo đồng hồ, nên một buổi diễn bắt đầu rất trễ vẫn có thể lên
        // sóng. Đợi quá hạn rồi mới kết luận.
        var seeded = await SeedAsync(LoungeShowFormat.Online, actualStart: null, endedHoursAgo: 1);

        await RunJobAsync();

        (await TicketStateAsync(seeded.LivestreamTicketId)).HasRefund.Should().BeFalse();
    }

    [Fact]
    public async Task ShowThuanOfflineThiJobNayKhongDungToi()
    {
        var seeded = await SeedAsync(
            LoungeShowFormat.Offline, actualStart: null, withLivestream: false);

        await RunJobAsync();

        (await TicketStateAsync(seeded.LivestreamTicketId)).HasRefund.Should().BeFalse(
            "show thuần offline không có bằng chứng nào ở đây cả — đó là đường xác minh riêng");
    }
}
