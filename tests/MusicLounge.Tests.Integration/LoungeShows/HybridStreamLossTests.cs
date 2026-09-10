using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MusicLounge.Application.Livestreams.Jobs;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Infrastructure.Persistence;
using MusicLounge.Tests.Integration.Helpers;

namespace MusicLounge.Tests.Integration.LoungeShows;

/// <summary>
/// MLACP-353. Với show <c>Hybrid</c> (phòng thật + livestream), khi stream dừng ngoài ý muốn — mất kết
/// nối quá hạn chờ, Admin gỡ, kiểm duyệt gỡ — cả buổi diễn từng bị đóng dù phòng vẫn đang diễn. Khán
/// giả tới muộn không check-in được (<c>CheckInTicket</c> đòi <c>Ongoing</c>) nên mất luôn quyền đánh
/// giá; và <c>EndLoungeShow</c> từ chối mọi show có livestream, nên nhân viên không có nút nào để đóng
/// buổi diễn cho đúng lúc.
/// </summary>
[Collection("Integration")]
public sealed class HybridStreamLossTests
{
    private readonly ApiFactory _factory;

    public HybridStreamLossTests(ApiFactory factory) => _factory = factory;

    private sealed record Seeded(int ShowId, int LivestreamId, DateTimeOffset? DisconnectedAt, string? QrCode);

    private async Task<Seeded> SeedAsync(LoungeShowFormat format, LivestreamStatus streamStatus)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var startedAt = DateTimeOffset.UtcNow.AddHours(-1);
        var show = new LoungeShow
        {
            LoungeId = SeedHelper.LoungeId,
            Name = $"Hybrid {Guid.NewGuid():N}"[..16],
            Format = format,
            Status = LoungeShowStatus.Ongoing,
            ScheduledStart = startedAt,
            ScheduledEnd = startedAt.AddHours(3),
            ActualStart = startedAt,
            CreatedAt = DateTime.UtcNow
        };
        db.Add(show);
        await db.SaveChangesAsync();

        DateTimeOffset? disconnectedAt = streamStatus == LivestreamStatus.Reconnecting
            ? DateTimeOffset.UtcNow.AddMinutes(-6)
            : null;
        var livestream = new Livestream
        {
            LoungeShowId = show.Id,
            Provider = "mux",
            ProviderRef = $"test-{Guid.NewGuid():N}",
            Status = streamStatus,
            StartedAt = startedAt,
            DisconnectedAt = disconnectedAt
        };
        db.Livestreams.Add(livestream);
        await db.SaveChangesAsync();

        string? qrCode = null;
        if (format != LoungeShowFormat.Online)
        {
            var tier = new TicketTier
            {
                LoungeShowId = show.Id, Name = "Physical", AccessType = AccessType.Physical,
                CreatedAt = DateTime.UtcNow
            };
            db.Add(tier);
            await db.SaveChangesAsync();
            var price = new TicketPrice
            {
                TierId = tier.Id, Name = "Đợt 1", Price = 300_000m,
                PurchaseChannel = PurchaseChannel.Online, SaleStart = DateTimeOffset.UtcNow.AddDays(-30)
            };
            db.Add(price);
            await db.SaveChangesAsync();
            var payment = new Payment
            {
                OrderId = $"MLACP353-{Guid.NewGuid():N}"[..30],
                PayerId = SeedHelper.AudienceId,
                GrossAmount = 300_000m, NetAmount = 300_000m,
                Status = PaymentStatus.Confirmed, ReferenceType = "TicketHold", ReferenceId = "0",
                TransactionId = $"H{Guid.NewGuid():N}"[..16],
                PaidAt = DateTimeOffset.UtcNow.AddDays(-2), CreatedAt = DateTimeOffset.UtcNow.AddDays(-2)
            };
            db.Add(payment);
            await db.SaveChangesAsync();

            qrCode = $"QR-{Guid.NewGuid():N}";
            db.Add(new Ticket
            {
                Id = Guid.NewGuid(),
                BuyerId = SeedHelper.AudienceId,
                PriceId = price.Id,
                TierId = tier.Id,
                ShowId = show.Id,
                PaymentId = payment.Id,
                Status = TicketStatus.Confirmed,
                QrCode = qrCode,
                PurchaseChannel = PurchaseChannel.Online,
                CreatedAt = DateTimeOffset.UtcNow.AddDays(-2)
            });
            await db.SaveChangesAsync();
        }

        return new Seeded(show.Id, livestream.Id, disconnectedAt, qrCode);
    }

    private async Task ReconnectTimeoutAsync(Seeded seeded)
    {
        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<LivestreamReconnectTimeoutJob>()
            .ExecuteAsync(seeded.LivestreamId, seeded.DisconnectedAt!.Value);
    }

    private async Task<(LoungeShow Show, Livestream Stream)> StateAsync(Seeded seeded)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return (await db.LoungeShows.AsNoTracking().SingleAsync(s => s.Id == seeded.ShowId),
                await db.Livestreams.AsNoTracking().SingleAsync(l => l.Id == seeded.LivestreamId));
    }

    private HttpClient Staff() => _factory.CreateAuthenticatedClient(SeedHelper.StaffId, "Staff", SeedHelper.LoungeId);
    private HttpClient Admin() => _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin");

    private async Task<HttpResponseMessage> ModerationRemoveStreamAsync(int livestreamId)
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Add(new ContentReport
            {
                TargetType = ReportTargetType.Livestream,
                TargetId = livestreamId,
                ReporterId = SeedHelper.AudienceId,
                Reason = "Nội dung vi phạm",
                Status = ContentReportStatus.Open,
                CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-10)
            });
            await db.SaveChangesAsync();
        }

        return await Admin().PostAsJsonAsync("/api/v1/content-reports/resolve", new
        {
            TargetType = "Livestream", TargetId = livestreamId, Action = "Removed", Note = "Gỡ theo báo cáo"
        });
    }

    // ── Hybrid: stream dừng, phòng vẫn diễn ─────────────────────────────────

    [Fact]
    public async Task MatKetNoiQuaHanThiChiDungStreamPhongVanMoVaChuPhongTraDuocBao()
    {
        var seeded = await SeedAsync(LoungeShowFormat.Hybrid, LivestreamStatus.Reconnecting);

        await ReconnectTimeoutAsync(seeded);

        var (show, stream) = await StateAsync(seeded);
        stream.Status.Should().Be(LivestreamStatus.Failed);
        show.Status.Should().Be(LoungeShowStatus.Ongoing,
            "phòng thật vẫn đang diễn — trước đây mất stream là đóng cả buổi");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.Notifications.AnyAsync(n =>
                n.UserId == SeedHelper.OwnerId
                && n.Type == NotificationType.LivestreamCutShort
                && n.ReferenceId == seeded.ShowId.ToString()
                && n.Title == "Livestream đã dừng — buổi diễn tại phòng vẫn mở"))
            .Should().BeTrue("không ai bấm kết thúc thì buổi diễn nằm mở tới khi job tự đóng chạy");
    }

    [Fact]
    public async Task SauKhiStreamChetKhachToiMuonVanCheckInDuoc()
    {
        var seeded = await SeedAsync(LoungeShowFormat.Hybrid, LivestreamStatus.Reconnecting);
        await ReconnectTimeoutAsync(seeded);

        var res = await Staff().PostAsJsonAsync("/api/v1/tickets/check-in", new { QrCode = seeded.QrCode });

        res.IsSuccessStatusCode.Should().BeTrue(
            "trước đây: 'Chỉ có thể check-in khi buổi diễn đang diễn ra' — khách tới cửa bị từ chối");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.Tickets.SingleAsync(t => t.QrCode == seeded.QrCode)).Status.Should().Be(TicketStatus.Used,
            "check-in cũng là thứ cho khán giả quyền đánh giá buổi diễn");
    }

    [Fact]
    public async Task NhanVienKetThucDuocBuoiDienKhiStreamDaDung()
    {
        var seeded = await SeedAsync(LoungeShowFormat.Hybrid, LivestreamStatus.Reconnecting);
        await ReconnectTimeoutAsync(seeded);

        (await Staff().PostAsync($"/api/v1/lounge-shows/{seeded.ShowId}/end", null)).StatusCode
            .Should().Be(HttpStatusCode.NoContent,
                "trước đây EndLoungeShow từ chối mọi show có livestream — không có nút nào để đóng buổi");

        var (show, _) = await StateAsync(seeded);
        show.Status.Should().Be(LoungeShowStatus.Ended);
        show.ActualEnd.Should().NotBeNull();
        show.RatingOpenUntil.Should().NotBeNull();
    }

    [Fact]
    public async Task StreamConPhatThiKhongKetThucBangNutCuaBuoiDien()
    {
        var seeded = await SeedAsync(LoungeShowFormat.Hybrid, LivestreamStatus.Live);

        (await Staff().PostAsync($"/api/v1/lounge-shows/{seeded.ShowId}/end", null)).StatusCode
            .Should().Be(HttpStatusCode.UnprocessableEntity,
                "stream còn chạy thì phải kết thúc bằng nút kết thúc stream");
    }

    [Fact]
    public async Task AdminDungStreamCuaShowHybridThiPhongVanMo()
    {
        var seeded = await SeedAsync(LoungeShowFormat.Hybrid, LivestreamStatus.Live);

        (await Admin().PostAsJsonAsync($"/api/v1/livestreams/{seeded.LivestreamId}/terminate",
                new { Reason = "Vi phạm bản quyền" }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var (show, stream) = await StateAsync(seeded);
        stream.Status.Should().Be(LivestreamStatus.Terminated);
        show.Status.Should().Be(LoungeShowStatus.Ongoing,
            "Admin dừng được stream, không dừng được phòng thật — đóng buổi chỉ làm hại khán giả tại phòng");
    }

    [Fact]
    public async Task KiemDuyetGoStreamCuaShowHybridThiPhongVanMo()
    {
        var seeded = await SeedAsync(LoungeShowFormat.Hybrid, LivestreamStatus.Live);

        (await ModerationRemoveStreamAsync(seeded.LivestreamId)).IsSuccessStatusCode.Should().BeTrue();

        var (show, stream) = await StateAsync(seeded);
        stream.Status.Should().Be(LivestreamStatus.Terminated);
        show.Status.Should().Be(LoungeShowStatus.Ongoing);
    }

    // ── Show Online: stream là toàn bộ buổi diễn — giữ nguyên hành vi ───────

    [Fact]
    public async Task ShowOnlineMatKetNoiThiVanKetThucCaBuoi()
    {
        var seeded = await SeedAsync(LoungeShowFormat.Online, LivestreamStatus.Reconnecting);

        await ReconnectTimeoutAsync(seeded);

        (await StateAsync(seeded)).Show.Status.Should().Be(LoungeShowStatus.Ended);
    }

    [Fact]
    public async Task KiemDuyetGoStreamShowOnlineThiDongBuoiVaMoCuaSoDanhGia()
    {
        var seeded = await SeedAsync(LoungeShowFormat.Online, LivestreamStatus.Live);

        (await ModerationRemoveStreamAsync(seeded.LivestreamId)).IsSuccessStatusCode.Should().BeTrue();

        var (show, _) = await StateAsync(seeded);
        show.Status.Should().Be(LoungeShowStatus.Ended);
        show.RatingOpenUntil.Should().NotBeNull();
    }
}
