using MusicLounge.Domain.ValueObjects;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MusicLounge.Application.Common;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Notifications;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Infrastructure.Persistence;

namespace MusicLounge.Infrastructure.Jobs;

/// <summary>
/// MLACP-458. Dọn hai loại đơn poster không tự kết thúc được — cùng mẫu với <see cref="ExpireStuckStitchAttemptsJob"/>.
///
/// Máy trạm nằm ngoài tầm với của máy chủ (máy cá nhân, có thể tắt bất cứ lúc nào), nên không có cách nào biết nó còn
/// sống ngoài việc đặt hạn và chờ:
/// <list type="bullet">
/// <item><b>Đơn đang làm quá hạn thuê</b> → trả về hàng đợi để máy khác (hoặc chính nó sau khi bật lại) làm tiếp. Quá
/// <see cref="PosterQueue.MaxAttempts"/> lần thì đóng hẳn, vì lúc đó lỗi gần như chắc chắn nằm ở chính đơn.</item>
/// <item><b>Đơn nằm chờ quá lâu</b> vì không có máy trạm nào trực → đóng và báo chủ phòng trà, thay vì để họ chờ mãi một
/// tấm poster không bao giờ tới.</item>
/// </list>
/// Cả hai đều KHÔNG trừ hạn mức: <c>Failed</c> và <c>Expired</c> không nằm trong phép đếm của GeneratePosterCommandHandler.
/// </summary>
public sealed class ExpirePosterJobsJob
{
    private readonly ApplicationDbContext _ctx;
    private readonly INotificationService _notifications;
    private readonly ILogger<ExpirePosterJobsJob> _logger;

    public ExpirePosterJobsJob(
        ApplicationDbContext ctx, INotificationService notifications, ILogger<ExpirePosterJobsJob> logger)
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

        // Lọc trạng thái ở DB, so ngày tháng ở phía ứng dụng — cùng giới hạn của provider SQLite trong test mà cả
        // ExpireStuckDonationsJob lẫn ExpireStuckStitchAttemptsJob đang né theo cách này.
        var dangLam = (await _ctx.Set<AiPosterGeneration>()
                .Where(g => g.Status == AiPosterGenerationStatus.Rendering)
                .ToListAsync(ct))
            .Where(g => g.LeaseExpiresAt is not null && g.LeaseExpiresAt <= now)
            .ToList();

        var traVeHangDoi = 0;
        var dongHan = new List<AiPosterGeneration>();
        foreach (var job in dangLam)
        {
            job.ClaimedBy = null;
            job.ClaimedAt = null;
            job.LeaseExpiresAt = null;

            if (job.AttemptCount < PosterQueue.MaxAttempts)
            {
                job.Status = AiPosterGenerationStatus.Queued;
                traVeHangDoi++;
            }
            else
            {
                job.Status = AiPosterGenerationStatus.Failed;
                job.ErrorMessage = PosterQueue.ThongBaoMayTramKhongPhanHoi;
                dongHan.Add(job);
            }
        }

        var hetHanCho = (await _ctx.Set<AiPosterGeneration>()
                .Where(g => g.Status == AiPosterGenerationStatus.Queued)
                .ToListAsync(ct))
            .Where(g => g.CreatedAt <= now - PosterQueue.QueueTimeout)
            .ToList();

        foreach (var job in hetHanCho)
        {
            job.Status = AiPosterGenerationStatus.Expired;
            job.ErrorMessage = PosterQueue.ThongBaoHetHanCho;
        }

        if (dangLam.Count == 0 && hetHanCho.Count == 0) return;

        // INotificationService chỉ XẾP HÀNG bản ghi thông báo (giống ILedgerService.WriteJournalAsync) — chính
        // SaveChangesAsync bên dưới mới ghi xuống, và nó ghi cùng lượt với trạng thái đơn. Gọi sau SaveChangesAsync thì
        // thông báo rơi mất, vì không còn ai lưu nữa.
        foreach (var job in dongHan)
            await BaoChuPhongTraAsync(
                job, new SongNgu(PosterQueue.ThongBaoMayTramKhongPhanHoi, PosterQueue.ThongBaoMayTramKhongPhanHoiEn), ct);
        foreach (var job in hetHanCho)
            await BaoChuPhongTraAsync(
                job, new SongNgu(PosterQueue.ThongBaoHetHanCho, PosterQueue.ThongBaoHetHanChoEn), ct);

        await _ctx.SaveChangesAsync(ct);

        _logger.LogWarning(
            "Dọn hàng đợi poster: {Requeued} đơn trả về hàng đợi, {Closed} đơn đóng vì máy trạm không phản hồi, " +
            "{Expired} đơn hết hạn chờ.",
            traVeHangDoi, dongHan.Count, hetHanCho.Count);
    }

    private async Task BaoChuPhongTraAsync(AiPosterGeneration job, SongNgu body, CancellationToken ct)
        => await _notifications.NotifyAsync(
            job.OwnerId,
            NotificationType.PosterGenerationResult,
            new SongNgu("Chưa tạo được poster", "Poster could not be created"),
            body,
            NotificationReferenceTypes.Show,
            job.ShowId.ToString(),
            ct);
}
