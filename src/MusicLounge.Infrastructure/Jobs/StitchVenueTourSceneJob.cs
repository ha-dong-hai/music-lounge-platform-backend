using Hangfire;
using Microsoft.EntityFrameworkCore;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.Exceptions;
using MusicLounge.Infrastructure.Persistence;

namespace MusicLounge.Infrastructure.Jobs;

// Does the actual panorama stitching for StitchVenueTourSceneCommandHandler, which only creates
// the Pending VenueTourStitchAttempt row and enqueues this job — see that handler's
// INoTransactionCommand comment for why the row is guaranteed to exist by the time this runs.
public sealed class StitchVenueTourSceneJob
{
    private readonly ApplicationDbContext _ctx;
    private readonly IPanoramaStitchingService _stitcher;
    private readonly IFileStorageService _fileStorage;
    private readonly IImageModerationGate _moderationGate;
    private readonly ISystemConfigService _config;

    public StitchVenueTourSceneJob(
        ApplicationDbContext ctx, IPanoramaStitchingService stitcher, IFileStorageService fileStorage,
        IImageModerationGate moderationGate, ISystemConfigService config)
    {
        _ctx = ctx;
        _stitcher = stitcher;
        _fileStorage = fileStorage;
        _moderationGate = moderationGate;
        _config = config;
    }

    public async Task ExecuteAsync(
        int attemptId, int loungeId, IReadOnlyList<string> sourceImageUrls, string? name,
        IJobCancellationToken cancellationToken)
    {
        var ct = cancellationToken.ShutdownToken;

        var attempt = await _ctx.Set<VenueTourStitchAttempt>().FirstOrDefaultAsync(a => a.Id == attemptId, ct);
        // Row missing (deleted?) or already resolved (shouldn't normally happen - one job per
        // attempt) - nothing left to do.
        if (attempt is null || attempt.Status != VenueTourStitchStatus.Pending) return;

        byte[] imageBytes;
        try
        {
            imageBytes = await _stitcher.StitchAsync(sourceImageUrls, ct);
        }
        catch (ExternalServiceException ex)
        {
            attempt.Status = VenueTourStitchStatus.Failed;
            attempt.ErrorMessage = ex.Message;
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

        string imageUrl;
        await using (var stream = new MemoryStream(imageBytes))
        {
            imageUrl = await _fileStorage.SaveImageAsync(stream, "panorama.jpg", ct);
        }

        // Quota (MaxTourScenesSnapshot) was already checked at enqueue time in the handler - not
        // re-checked here. Going async widens an already-existing race (two concurrent requests
        // both passing the same "count < max" check) from a sub-millisecond window to however long
        // this job sits in queue, so a burst of near-simultaneous stitch requests could land one or
        // two scenes over quota. Not re-litigated here - same class of race the direct-upload path
        // (AddVenueTourSceneCommandHandler) already has, just a wider window; fixing it properly
        // needs a DB-level constraint or compensating rollback, out of scope for this pass.
        var existingSceneCount = await _ctx.Set<VenueTourScene>().CountAsync(s => s.LoungeId == loungeId, ct);
        var scene = new VenueTourScene
        {
            LoungeId = loungeId,
            ImageUrl = imageUrl,
            Name = name,
            OrderIndex = existingSceneCount
        };
        _ctx.Set<VenueTourScene>().Add(scene);

        attempt.Status = VenueTourStitchStatus.Succeeded;
        attempt.ResultScene = scene;

        await _ctx.SaveChangesAsync(ct);

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
                CreatedAt = now,
                SlaDeadline = now.AddHours(slaHours)
            });
            await _ctx.SaveChangesAsync(ct);
        }
    }
}
