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
/// MLACP-341. Sau MLACP-336/338/340 <b>tiền đã an toàn ở cả hai chiều</b>: quyết toán bị giữ lại,
/// người mua tự đòi lại được 100%, và vé livestream được hoàn tự động. Thứ còn thiếu là
/// <b>giao tiếp</b> — với vé Physical, người mua phải TỰ NHẬN RA rằng buổi diễn không diễn ra, trong
/// khi nền tảng đã biết từ giờ thứ sáu.
///
/// <para>Đó đúng là chỗ làm hỏng uy tín: người mua trả tiền cho nền tảng, nền tảng biết có vấn đề,
/// và im lặng.</para>
///
/// <para><b>Không tự động hoàn như vé livestream</b>, vì với vé offline nền tảng không phải kênh
/// giao hàng nên không kết luận được — buổi diễn có thể đã chạy thật mà phòng trà quên bấm Bắt đầu.
/// Hoàn cho cả khán phòng vì một nút không được bấm sẽ là sai lầm nặng hơn.</para>
///
/// <para><b>Hai chặng.</b> Báo chủ phòng trà trước; nếu buổi diễn có diễn ra thì họ liên hệ quản trị
/// viên và người mua không bị làm phiền lần nào.</para>
/// </summary>
[Collection("Integration")]
public sealed class UndeliveredOfflineShowNoticeTests
{
    private readonly ApiFactory _factory;

    public UndeliveredOfflineShowNoticeTests(ApiFactory factory) => _factory = factory;

    private async Task<int> SeedAsync(
        AccessType accessType, DateTimeOffset? actualStart, int endedHoursAgo)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var end = DateTimeOffset.UtcNow.AddHours(-endedHoursAgo);

        var show = new LoungeShow
        {
            LoungeId = SeedHelper.LoungeId,
            Name = $"Show {Guid.NewGuid():N}"[..18],
            Format = accessType == AccessType.Physical
                ? LoungeShowFormat.Offline
                : LoungeShowFormat.Online,
            Status = LoungeShowStatus.Ended,
            ScheduledStart = end.AddHours(-2),
            ScheduledEnd = end,
            ActualStart = actualStart,
            ActualEnd = end,
            CreatedAt = DateTime.UtcNow
        };
        db.Add(show);
        await db.SaveChangesAsync();

        var tier = new TicketTier
        {
            LoungeShowId = show.Id,
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
            Price = 250_000m,
            PurchaseChannel = PurchaseChannel.Online,
            SaleStart = DateTimeOffset.UtcNow.AddDays(-30)
        };
        db.Add(price);
        await db.SaveChangesAsync();

        db.Add(new Ticket
        {
            Id = Guid.NewGuid(),
            BuyerId = SeedHelper.AudienceId,
            PriceId = price.Id,
            TierId = tier.Id,
            ShowId = show.Id,
            Status = TicketStatus.Confirmed,
            PurchaseChannel = PurchaseChannel.Online,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-2)
        });
        await db.SaveChangesAsync();

        return show.Id;
    }

    private async Task RunJobAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var job = scope.ServiceProvider.GetRequiredService<NotifyUndeliveredOfflineShowJob>();
        await job.ExecuteAsync(new JobCancellationToken(false));
    }

    private async Task<int> ToldCountAsync(int userId, int showId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Notifications.CountAsync(n =>
            n.UserId == userId
            && n.Type == NotificationType.ShowDeliveryUnconfirmed
            && n.ReferenceType == "show"
            && n.ReferenceId == showId.ToString());
    }

    // ── Chặng một: phòng trà được hỏi trước ─────────────────────────────────

    [Fact]
    public async Task ChuPhongTraDuocBaoTruoc()
    {
        var showId = await SeedAsync(AccessType.Physical, actualStart: null, endedHoursAgo: 12);

        await RunJobAsync();

        (await ToldCountAsync(SeedHelper.OwnerId, showId)).Should().Be(1);
    }

    [Fact]
    public async Task TrongCuaSoCuaPhongTraThiNguoiMuaChuaBiLamPhien()
    {
        // Một lần quên bấm nút không được biến thành một tin xấu gửi cho cả khán phòng của một đêm
        // đã diễn bình thường.
        var showId = await SeedAsync(AccessType.Physical, actualStart: null, endedHoursAgo: 12);

        await RunJobAsync();

        (await ToldCountAsync(SeedHelper.AudienceId, showId)).Should().Be(0);
    }

    // ── Chặng hai: hết cửa sổ thì người mua phải được biết ──────────────────

    [Fact]
    public async Task HetCuaSoMaChuaGiaiQuyetThiNguoiMuaDuocBao()
    {
        // 6 tiếng biên an toàn + 24 tiếng cửa sổ của phòng trà = 30; lấy 40 cho chắc.
        var showId = await SeedAsync(AccessType.Physical, actualStart: null, endedHoursAgo: 40);

        await RunJobAsync();

        (await ToldCountAsync(SeedHelper.AudienceId, showId)).Should().Be(1,
            "người mua trả tiền cho nền tảng — nền tảng biết có vấn đề mà im lặng mới là chỗ làm " +
            "hỏng uy tín");
    }

    [Fact]
    public async Task ChayNhieuLanCungChiBaoMotLanChoMoiNguoi()
    {
        var showId = await SeedAsync(AccessType.Physical, actualStart: null, endedHoursAgo: 40);

        await RunJobAsync();
        await RunJobAsync();
        await RunJobAsync();

        (await ToldCountAsync(SeedHelper.OwnerId, showId)).Should().Be(1);
        (await ToldCountAsync(SeedHelper.AudienceId, showId)).Should().Be(1);
    }

    // ── Không đụng tới những gì không thuộc phạm vi ─────────────────────────

    [Fact]
    public async Task BuoiDienDaDienRaThatThiKhongBaoAiCa()
    {
        var start = DateTimeOffset.UtcNow.AddHours(-42);
        var showId = await SeedAsync(AccessType.Physical, actualStart: start, endedHoursAgo: 40);

        await RunJobAsync();

        (await ToldCountAsync(SeedHelper.OwnerId, showId)).Should().Be(0);
        (await ToldCountAsync(SeedHelper.AudienceId, showId)).Should().Be(0);
    }

    [Fact]
    public async Task VeLivestreamKhongDiDuongNay()
    {
        // Vé livestream đã được hoàn tự động ở MLACP-340 vì ở đó bằng chứng kết luận được. Báo thêm
        // ở đây là nói với họ rằng "chưa xác nhận được" trong khi đã hoàn tiền cho họ rồi.
        var showId = await SeedAsync(AccessType.Livestream, actualStart: null, endedHoursAgo: 40);

        await RunJobAsync();

        (await ToldCountAsync(SeedHelper.AudienceId, showId)).Should().Be(0);
        (await ToldCountAsync(SeedHelper.OwnerId, showId)).Should().Be(0);
    }

    [Fact]
    public async Task ChuaQuaBienAnToanThiChuaBaoAi()
    {
        var showId = await SeedAsync(AccessType.Physical, actualStart: null, endedHoursAgo: 1);

        await RunJobAsync();

        (await ToldCountAsync(SeedHelper.OwnerId, showId)).Should().Be(0);
    }
}
