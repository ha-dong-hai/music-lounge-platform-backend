using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Enums;
using MusicLounge.Infrastructure.Persistence;

namespace MusicLounge.Infrastructure.Jobs;

/// <summary>
/// Gỡ lệnh tạm khoá phòng trà khi đã phục vụ đủ hạn.
///
/// Nửa còn thiếu của <see cref="ApplyDuePenaltiesJob"/>. Job đó áp lệnh khoá và cộng bù hạn
/// subscription, rồi dừng ở đó — không có gì đưa phòng trà trở lại. Đường duy nhất quay về
/// <see cref="LoungeStatus.Approved"/> là qua khiếu nại, nên phòng trà nào không khiếu nại thì bị
/// khoá vĩnh viễn, trong khi thông báo gửi cho họ ghi rõ "sẽ bị tạm khoá N ngày".
///
/// Model vốn đã lường trước điều này — có sẵn cột SuspensionEnd và giá trị PenaltyStatus.Expired —
/// chỉ là chưa ai cài đặt.
/// </summary>
public sealed class ExpireServedSuspensionsJob
{
    private readonly ApplicationDbContext _ctx;
    private readonly INotificationService _notifications;
    private readonly ILogger<ExpireServedSuspensionsJob> _logger;

    public ExpireServedSuspensionsJob(
        ApplicationDbContext ctx, INotificationService notifications,
        ILogger<ExpireServedSuspensionsJob> logger)
    {
        _ctx = ctx;
        _notifications = notifications;
        _logger = logger;
    }

    [DisableConcurrentExecution(timeoutInSeconds: 30)]
    public async Task ExecuteAsync(IJobCancellationToken cancellationToken)
    {
        var ct = cancellationToken.ShutdownToken;
        var now = DateTimeOffset.UtcNow;

        // Lọc enum ở phía database rồi so mốc thời gian phía client — kết hợp cả hai trong một truy
        // vấn không dịch được sang SQLite (provider dùng trong test), giới hạn đã ghi nhận khắp
        // codebase này.
        var applied = await _ctx.VenuePenalties
            .Where(p => p.Status == PenaltyStatus.Active
                        && p.PenaltyType == PenaltyType.Suspension
                        && p.AppliedAt != null)
            .ToListAsync(ct);

        foreach (var penalty in applied)
        {
            // Án phạt áp trước khi cột này được ghi có SuspensionEnd rỗng. Không suy ra được mốc
            // hết hạn cho chúng thì đúng những phòng trà đang bị khoá hôm nay lại là những phòng
            // không bao giờ được gỡ — tức là bản sửa này không sửa được gì cho ai. Suy ra từ thời
            // điểm áp lệnh, rồi ghi lại luôn để lần sau khỏi phải đoán và để chủ phòng trà nhìn
            // thấy hạn trên màn hình án phạt của họ.
            if (penalty.SuspensionEnd is null && penalty.SuspensionDays is int legacyDays)
                penalty.SuspensionEnd = penalty.AppliedAt!.Value.AddDays(legacyDays);
        }

        // SuspensionDays rỗng nghĩa là khoá không thời hạn — không phải thứ tự hết hạn được.
        var served = applied.Where(p => p.SuspensionEnd is not null && p.SuspensionEnd <= now).ToList();

        if (served.Count == 0)
        {
            await _ctx.SaveChangesAsync(ct);   // vẫn lưu phần suy ra mốc hết hạn ở trên
            return;
        }

        foreach (var penalty in served)
        {
            penalty.Status = PenaltyStatus.Expired;

            var lounge = await _ctx.Lounges.FirstOrDefaultAsync(l => l.Id == penalty.LoungeId, ct);
            if (lounge is null) continue;

            // Cùng quy tắc ReviewAppealCommandHandler đang dùng: chỉ trả phòng trà về trạng thái
            // bình thường khi không còn án phạt nào khác đang thi hành. Hai lệnh treo chồng nhau mà
            // gỡ theo cái hết hạn trước thì phòng trà được thả sớm hơn mức đáng bị.
            var otherActive = await _ctx.VenuePenalties.CountAsync(
                p => p.LoungeId == lounge.Id
                     && p.Id != penalty.Id
                     && p.Status == PenaltyStatus.Active
                     && p.AppliedAt != null
                     && (p.PenaltyType == PenaltyType.Suspension || p.PenaltyType == PenaltyType.Ban),
                ct);

            // Chỉ đụng vào phòng trà đang thực sự bị treo. Nếu Admin đã chuyển nó sang trạng thái
            // khác bằng tay thì quyết định đó thắng — job này không ghi đè.
            var restored = otherActive == 0 && lounge.Status == LoungeStatus.Suspended;
            if (restored)
                lounge.Status = LoungeStatus.Approved;

            await _ctx.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Suspension served and expired: PenaltyId={PenaltyId} LoungeId={LoungeId} " +
                "Restored={Restored} OtherActivePenalties={OtherActive} at {At}",
                penalty.Id, lounge.Id, restored, otherActive, now);

            await _notifications.NotifyAsync(
                lounge.OwnerId,
                NotificationType.PenaltyExpired,
                restored ? "Phòng trà đã được mở khoá" : "Lệnh tạm khoá đã hết hạn",
                restored
                    ? $"\"{lounge.Name}\" đã hết hạn tạm khoá theo phạt #{penalty.Id} và hoạt động trở lại bình thường."
                    : $"Lệnh tạm khoá theo phạt #{penalty.Id} đã hết hạn, nhưng \"{lounge.Name}\" vẫn đang chịu một án phạt khác nên chưa mở khoá.",
                referenceType: "venue_penalty",
                referenceId: penalty.Id.ToString(),
                ct: ct);

            await _ctx.SaveChangesAsync(ct);
        }
    }
}
