using Hangfire;
using Microsoft.EntityFrameworkCore;
using MusicLounge.Application.Common;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Enums;
using MusicLounge.Infrastructure.Persistence;

namespace MusicLounge.Infrastructure.Jobs;

/// <summary>
/// MLACP-339 — nhắc chủ phòng trà bấm Bắt đầu khi đã tới giờ diễn mà buổi diễn còn ở
/// <c>Published</c>.
///
/// <para>Bấm Bắt đầu không phải thao tác ghi nhận cho vui. Nó là điều kiện <b>bắt buộc</b> của ba
/// thứ: <c>CheckInTicket</c>, <c>CreateDonation</c> và (gián tiếp qua vé <c>Used</c>)
/// <c>RateShow</c> đều đòi <c>Status == Ongoing</c>. Một phòng trà quên bấm đang có một đêm diễn
/// hỏng toàn bộ tính năng — nhân viên không quét được vé ở cửa, khán giả không donate được, và sau
/// đó không ai đánh giá được.</para>
///
/// <para>Từ MLACP-336 và MLACP-338, việc quên bấm còn có hậu quả tiền bạc thật: cả hai tranche
/// quyết toán bị giữ lại, và người mua tự đòi lại được 100% tiền vé kể cả khi phòng trà đặt chính
/// sách cấm huỷ. Một thông báo lúc tới giờ ngăn được gần như toàn bộ lớp vấn đề đó — và đó cũng là
/// cách bảo vệ uy tín tốt nhất, vì người mua không bao giờ phải nhận cái tin "buổi diễn của bạn có
/// thể đã không diễn ra".</para>
/// </summary>
public sealed class RemindOwnerToStartShowJob
{
    /// <summary>Mặc định khi <c>system_config</c> chưa có khoá.</summary>
    public const int DefaultReminderMinutes = 15;

    private readonly ApplicationDbContext _ctx;
    private readonly ISystemConfigService _config;
    private readonly INotificationService _notifications;

    public RemindOwnerToStartShowJob(
        ApplicationDbContext ctx, ISystemConfigService config, INotificationService notifications)
    {
        _ctx = ctx;
        _config = config;
        _notifications = notifications;
    }

    [DisableConcurrentExecution(timeoutInSeconds: 30)]
    public async Task ExecuteAsync(IJobCancellationToken cancellationToken)
    {
        var ct = cancellationToken.ShutdownToken;
        var now = DateTimeOffset.UtcNow;

        var lateMinutes = await _config.GetIntAsync(
            ConfigKeys.ShowStartReminderMinutes, DefaultReminderMinutes, ct);

        // Lọc trạng thái phía server, so thời gian phía client — cùng giới hạn của provider SQLite
        // đã ghi khắp thư mục này: một phép so DateTimeOffset ghép với điều kiện enum trong cùng
        // một Where không dịch được.
        var candidates = await _ctx.LoungeShows
            .Where(s => s.Status == LoungeShowStatus.Published)
            .ToListAsync(ct);

        var late = candidates
            .Where(s => s.ScheduledStart.AddMinutes(lateMinutes) <= now
                        // Quá giờ kết thúc thì thôi: lúc đó không còn gì để cứu, và đó là địa phận
                        // của AutoEndStaleShowsJob và chốt hoàn tiền ở MLACP-338. Nhắc tiếp chỉ là
                        // báo một tin xấu mà người nhận không làm gì được nữa.
                        && now <= ShowSchedule.EffectiveEnd(s))
            .ToList();

        if (late.Count == 0) return;

        var loungeIds = late.Select(s => s.LoungeId).Distinct().ToList();
        var ownerByLounge = await _ctx.Lounges
            .Where(l => loungeIds.Contains(l.Id))
            .Select(l => new { l.Id, l.OwnerId })
            .ToDictionaryAsync(l => l.Id, l => l.OwnerId, ct);

        var notified = false;

        foreach (var show in late)
        {
            if (!ownerByLounge.TryGetValue(show.LoungeId, out var ownerId)) continue;

            // Chống trùng bằng chính dòng thông báo đã gửi, không thêm cột cờ mới — đúng cách
            // EventReminderJob làm. Job chạy mỗi phút nên thiếu chốt này là gửi mỗi phút một lần.
            var alreadyReminded = await _ctx.Notifications.AnyAsync(
                n => n.UserId == ownerId
                     && n.Type == NotificationType.ShowNotStarted
                     && n.ReferenceType == "show"
                     && n.ReferenceId == show.Id.ToString(), ct);
            if (alreadyReminded) continue;

            await _notifications.NotifyAsync(
                ownerId,
                NotificationType.ShowNotStarted,
                "Buổi diễn chưa được bắt đầu",
                $"\"{show.Name}\" đã tới giờ diễn nhưng chưa được bấm Bắt đầu. Chưa bấm thì nhân " +
                "viên không quét được vé ở cửa, khán giả không donate được, và sau đó không ai " +
                "đánh giá được buổi diễn.",
                referenceType: "show",
                referenceId: show.Id.ToString(),
                ct: ct);

            notified = true;
        }

        // NotifyAsync mới chỉ Add vào change tracker — hợp đồng ghi rõ người gọi phải lưu.
        if (notified) await _ctx.SaveChangesAsync(ct);
    }
}
