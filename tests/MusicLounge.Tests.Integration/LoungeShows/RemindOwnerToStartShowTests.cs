using FluentAssertions;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Infrastructure.Jobs;
using MusicLounge.Infrastructure.Persistence;
using MusicLounge.Tests.Integration.Helpers;

namespace MusicLounge.Tests.Integration.LoungeShows;

/// <summary>
/// MLACP-339. Bấm "Bắt đầu" là điều kiện <b>bắt buộc</b> của ba thứ — <c>CheckInTicket</c>,
/// <c>CreateDonation</c> và (gián tiếp qua vé <c>Used</c>) <c>RateShow</c> đều đòi
/// <c>Status == Ongoing</c>. Quên bấm không phải là lệch một cột dữ liệu: nhân viên không quét được
/// vé ở cửa, khán giả không donate được, và sau đó không ai đánh giá được.
///
/// <para>Từ MLACP-336 và MLACP-338, quên bấm còn có hậu quả tiền bạc thật: cả hai tranche quyết
/// toán bị giữ lại, và người mua tự đòi lại được 100% tiền vé kể cả khi phòng trà đặt chính sách
/// cấm huỷ.</para>
///
/// <para><c>EventReminderJob</c> chỉ nhắc <b>người mua vé</b> trước giờ diễn — không job nào nhắc
/// chủ phòng trà.</para>
/// </summary>
[Collection("Integration")]
public sealed class RemindOwnerToStartShowTests
{
    private readonly ApiFactory _factory;

    public RemindOwnerToStartShowTests(ApiFactory factory) => _factory = factory;

    private async Task<int> SeedShowAsync(
        LoungeShowStatus status, DateTimeOffset scheduledStart, DateTimeOffset scheduledEnd)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var show = new LoungeShow
        {
            LoungeId = SeedHelper.LoungeId,
            Name = $"Show {Guid.NewGuid():N}"[..18],
            Status = status,
            ScheduledStart = scheduledStart,
            ScheduledEnd = scheduledEnd,
            CreatedAt = DateTime.UtcNow
        };
        db.Add(show);
        await db.SaveChangesAsync();
        return show.Id;
    }

    private async Task RunJobAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var job = scope.ServiceProvider.GetRequiredService<RemindOwnerToStartShowJob>();
        await job.ExecuteAsync(new JobCancellationToken(false));
    }

    private async Task<int> ReminderCountAsync(int showId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Notifications.CountAsync(n =>
            n.UserId == SeedHelper.OwnerId
            && n.Type == NotificationType.ShowNotStarted
            && n.ReferenceType == "show"
            && n.ReferenceId == showId.ToString());
    }

    [Fact]
    public async Task QuaGioDienMaChuaBamBatDauThiChuPhongTraDuocNhac()
    {
        var start = DateTimeOffset.UtcNow.AddMinutes(-30);
        var showId = await SeedShowAsync(LoungeShowStatus.Published, start, start.AddHours(2));

        await RunJobAsync();

        (await ReminderCountAsync(showId)).Should().Be(1,
            "chưa bấm Bắt đầu thì nhân viên không quét được vé ở cửa và khán giả không donate được");
    }

    [Fact]
    public async Task ChayNhieuLanCungChiNhacMotLan()
    {
        // Job chạy mỗi phút — thiếu chốt chống trùng là gửi mỗi phút một thông báo.
        var start = DateTimeOffset.UtcNow.AddMinutes(-30);
        var showId = await SeedShowAsync(LoungeShowStatus.Published, start, start.AddHours(2));

        await RunJobAsync();
        await RunJobAsync();
        await RunJobAsync();

        (await ReminderCountAsync(showId)).Should().Be(1);
    }

    [Fact]
    public async Task ChuaToiNguongThiChuaNhac()
    {
        // Bắt đầu muộn vài phút là chuyện bình thường — báo động ngay là làm phiền.
        var start = DateTimeOffset.UtcNow.AddMinutes(-2);
        var showId = await SeedShowAsync(LoungeShowStatus.Published, start, start.AddHours(2));

        await RunJobAsync();

        (await ReminderCountAsync(showId)).Should().Be(0);
    }

    [Fact]
    public async Task DaBamBatDauRoiThiKhongNhac()
    {
        var start = DateTimeOffset.UtcNow.AddMinutes(-30);
        var showId = await SeedShowAsync(LoungeShowStatus.Ongoing, start, start.AddHours(2));

        await RunJobAsync();

        (await ReminderCountAsync(showId)).Should().Be(0);
    }

    [Fact]
    public async Task QuaGioKetThucRoiThiThoiKhongNhacNua()
    {
        // Lúc này không còn gì để cứu — đó là địa phận của AutoEndStaleShowsJob và chốt hoàn tiền
        // ở MLACP-338. Nhắc tiếp chỉ là báo một tin xấu mà người nhận không làm gì được nữa.
        var start = DateTimeOffset.UtcNow.AddHours(-5);
        var showId = await SeedShowAsync(LoungeShowStatus.Published, start, start.AddHours(2));

        await RunJobAsync();

        (await ReminderCountAsync(showId)).Should().Be(0);
    }
}
