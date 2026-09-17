using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Lounges;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.Exceptions;
using MusicLounge.Infrastructure.Persistence;

namespace MusicLounge.Infrastructure.Jobs;

// Does the actual panorama stitching for StitchVenueTourSceneCommandHandler, which only creates
// the Pending VenueTourStitchAttempt row and enqueues this job — see that handler's
// INoTransactionCommand comment for why the row is guaranteed to exist by the time this runs.
//
// MLACP-435: KHONG thu lai. Truoc day khong khai bao gi nen Hangfire dung mac dinh 10 lan keo dai nhieu gio: mot loi
// khong luong truoc (luu file loi...) lam job ghep lai tu dau toi 10 lan, lan nao cung dot CPU, va het luot thi luot ghep
// nam o Pending mai mai (chu phong tra cho vo han, luot van bi dem vao gioi han). Khong co gi dang de thu lai tu dong:
// loi cua bo anh thi thu lai van loi; loi dich vu thi dich vu da tu danh thuc/thu lai ben trong; loi ket noi DB thoang
// qua da co execution strategy cua EF (MLACP-415). Chu phong tra tu bam ghep lai — luot loi he thong khong bi tinh.
[AutomaticRetry(Attempts = 0, OnAttemptsExceeded = AttemptsExceededAction.Fail)]
public sealed class StitchVenueTourSceneJob
{
    internal const string ThongBaoGianDoan =
        "Lượt ghép bị gián đoạn do sự cố hệ thống và không bị tính vào giới hạn số lần ghép. Vui lòng thử lại.";

    private readonly ApplicationDbContext _ctx;
    private readonly IPanoramaStitchingService _stitcher;
    private readonly IFileStorageService _fileStorage;
    private readonly IImageModerationGate _moderationGate;
    private readonly ISystemConfigService _config;
    private readonly ILogger<StitchVenueTourSceneJob> _logger;
    private readonly IAsyncKeyedLock _lock;

    public StitchVenueTourSceneJob(
        ApplicationDbContext ctx, IPanoramaStitchingService stitcher, IFileStorageService fileStorage,
        IImageModerationGate moderationGate, ISystemConfigService config, ILogger<StitchVenueTourSceneJob> logger,
        IAsyncKeyedLock @lock)
    {
        _ctx = ctx;
        _stitcher = stitcher;
        _fileStorage = fileStorage;
        _moderationGate = moderationGate;
        _config = config;
        _logger = logger;
        _lock = @lock;
    }

    public async Task ExecuteAsync(
        int attemptId, int loungeId, IReadOnlyList<string> sourceImageUrls, string? name,
        IJobCancellationToken cancellationToken)
    {
        var ct = cancellationToken.ShutdownToken;

        var attempt = await _ctx.Set<VenueTourStitchAttempt>().FirstOrDefaultAsync(a => a.Id == attemptId, ct);
        // Row missing (deleted?) or already resolved (shouldn't normally happen - one job per
        // attempt; or ExpireStuckStitchAttemptsJob already gave up on it) - nothing left to do.
        if (attempt is null || attempt.Status != VenueTourStitchStatus.Pending) return;

        try
        {
            await GhepAsync(attempt, loungeId, sourceImageUrls, name, ct);
        }
        catch (Exception ex) when (!(ex is OperationCanceledException && ct.IsCancellationRequested))
        {
            // Server dang tat (deploy) thi de nguyen Pending: Hangfire dua job vao hang doi lai khi khoi dong. Moi loi
            // khac: dong luot ghep lai de chu phong tra khong cho vo han, roi nem tiep de job hien Failed tren dashboard.
            _logger.LogError(ex, "Lượt ghép ảnh {AttemptId} lỗi không lường trước — đánh dấu thất bại do hệ thống.", attemptId);
            await DanhDauThatBaiDoHeThongAsync(attemptId);
            throw;
        }
    }

    private async Task GhepAsync(
        VenueTourStitchAttempt attempt, int loungeId, IReadOnlyList<string> sourceImageUrls, string? name,
        CancellationToken ct)
    {
        byte[] imageBytes;
        try
        {
            imageBytes = await _stitcher.StitchAsync(sourceImageUrls, ct);
        }
        catch (DomainException ex)
        {
            // Dich vu da xu ly bo anh nhung khong ghep duoc / ghep qua lau — CPU da chay, tinh vao gioi han.
            attempt.Status = VenueTourStitchStatus.Failed;
            attempt.ErrorMessage = ex.Message;
            await _ctx.SaveChangesAsync(ct);
            return;
        }
        catch (ExternalServiceException ex)
        {
            attempt.Status = VenueTourStitchStatus.Failed;
            // MLACP-435: loi phia he thong — khong tinh vao gioi han so lan ghep.
            attempt.FailedBySystem = true;
            // MLACP-432: Detail, khong phai Message — chu phong tra doc loi nay, khong can tien to "[PanoramaStitcher]".
            attempt.ErrorMessage = ex.Detail;
            await _ctx.SaveChangesAsync(ct);
            return;
        }

        // No HTTP caller to throw to here (runs in the background) - a block is reported as a
        // Failed attempt instead, same as any other stitch failure the Owner needs to see via polling.
        AiModerationResult? moderation;
        try
        {
            moderation = await _moderationGate.CheckOrThrowAsync(imageBytes, "image/jpeg", ct);
        }
        catch (DomainException ex)
        {
            attempt.Status = VenueTourStitchStatus.Failed;
            attempt.ErrorMessage = ex.Message;
            await _ctx.SaveChangesAsync(ct);
            return;
        }

        // MLACP-436: khoa theo phong tra (cung khoa voi AddVenueTourSceneCommandHandler) va KIEM LAI gioi han ngay truoc
        // khi tao canh. Job co the nam cho vai phut sau khi handler kiem: trong luc do chu phong tra co the them canh
        // khac hoac goi het han. Truoc day job khong kiem lai nen co the vuot so canh cua goi. Kiem TRUOC khi luu file de
        // bi tu choi thi khong de lai file mo coi. Job khong chay trong transaction nen khoa nha khi ra khoi khoi nay —
        // sau SaveChangesAsync.
        VenueTourScene scene;
        await using (await _lock.AcquireAsync(VenueTourRules.LockKey(loungeId), ct))
        {
            var ownerId = await _ctx.Lounges.Where(l => l.Id == loungeId).Select(l => l.OwnerId).FirstAsync(ct);
            var subscriptions = await _ctx.OwnerSubscriptions
                .Where(s => s.OwnerId == ownerId && s.Status == SubscriptionStatus.Active)
                .ToListAsync(ct);
            var existingScenes = await _ctx.Set<VenueTourScene>().Where(s => s.LoungeId == loungeId).ToListAsync(ct);

            if (VenueTourRules.QuotaViolation(
                    existingScenes.Count, VenueTourRules.MaxScenes(subscriptions, DateTimeOffset.UtcNow)) is { } loi)
            {
                // Khong phai loi he thong: gioi han cua goi, va CPU da ghep xong — tinh vao gioi han so lan ghep.
                attempt.Status = VenueTourStitchStatus.Failed;
                attempt.ErrorMessage = loi;
                await _ctx.SaveChangesAsync(ct);
                return;
            }

            string imageUrl;
            await using (var stream = new MemoryStream(imageBytes))
            {
                imageUrl = await _fileStorage.SaveImageAsync(stream, "panorama.jpg", ct);
            }

            scene = new VenueTourScene
            {
                LoungeId = loungeId,
                ImageUrl = imageUrl,
                Name = name,
                OrderIndex = VenueTourRules.NextOrderIndex(existingScenes)
            };
            _ctx.Set<VenueTourScene>().Add(scene);

            attempt.Status = VenueTourStitchStatus.Succeeded;
            // ExpireStuckStitchAttemptsJob co the da danh dau luot nay trong luc job con chay — ket qua that la thanh cong.
            attempt.FailedBySystem = false;
            attempt.ErrorMessage = null;
            attempt.ResultScene = scene;

            await _ctx.SaveChangesAsync(ct);
        }

        if (moderation is not null)
        {
            var slaHours = await _config.GetIntAsync(ConfigKeys.ModerationSlaHours, 24, ct);
            var now = DateTimeOffset.UtcNow;
            _ctx.Set<EventModeration>().Add(new EventModeration
            {
                TargetType = ModerationTargetType.TourScene,
                TargetId = scene.Id,
                AiScore = moderation.Score,
                RiskLevel = Enum.TryParse<ModerationRiskLevel>(moderation.RiskLevel, true, out var risk) ? risk : null,
                FlagReason = moderation.FlagReason,
                AiRecommendation = Enum.TryParse<AiModerationRecommendation>(moderation.Recommendation, true, out var rec) ? rec : null,
                SlaDeadline = now.AddHours(slaHours)
            });
            await _ctx.SaveChangesAsync(ct);
        }
    }

    /// <summary>
    /// Doc lai luot ghep tren mot ChangeTracker sach (loi vua roi co the la DbUpdateException, de lai thay doi hong) va
    /// chi dong khi con Pending: loi xay ra SAU khi da ghi Succeeded (vd luc ghi ban ghi kiem duyet) thi canh da tao
    /// that, khong duoc bao that bai. Tu than no loi thi chi ghi log — ExpireStuckStitchAttemptsJob se don sau.
    /// </summary>
    private async Task DanhDauThatBaiDoHeThongAsync(int attemptId)
    {
        try
        {
            _ctx.ChangeTracker.Clear();
            var attempt = await _ctx.Set<VenueTourStitchAttempt>().FirstOrDefaultAsync(a => a.Id == attemptId);
            if (attempt is null || attempt.Status != VenueTourStitchStatus.Pending) return;

            attempt.Status = VenueTourStitchStatus.Failed;
            attempt.FailedBySystem = true;
            attempt.ErrorMessage = ThongBaoGianDoan;
            await _ctx.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Không đánh dấu được lượt ghép {AttemptId} là thất bại — job dọn lượt kẹt sẽ xử lý.", attemptId);
        }
    }
}
