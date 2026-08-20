using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Infrastructure.Persistence;

namespace MusicLounge.Infrastructure.Jobs;

// Populates the AI-scoring fields on a freshly-created EventModeration row (AiScore/RiskLevel/
// FlagReason/AiRecommendation) — previously always null, despite GetPendingModerations already
// ordering the Admin review queue by AiScore descending. Runs as a background job, not inline in
// PublishLoungeShowCommandHandler/CreateLivestreamCommandHandler, so a slow or unavailable AI
// vendor never delays the Owner's publish/create-livestream response.
public sealed class ScoreModerationWithAiJob
{
    private readonly ApplicationDbContext _ctx;
    private readonly IAiModerationService _aiModeration;
    private readonly ILogger<ScoreModerationWithAiJob> _logger;

    public ScoreModerationWithAiJob(
        ApplicationDbContext ctx, IAiModerationService aiModeration, ILogger<ScoreModerationWithAiJob> logger)
    {
        _ctx = ctx;
        _aiModeration = aiModeration;
        _logger = logger;
    }

    [DisableConcurrentExecution(timeoutInSeconds: 30)]
    public async Task ExecuteAsync(int moderationId, IJobCancellationToken cancellationToken)
    {
        var ct = cancellationToken.ShutdownToken;

        var moderation = await _ctx.EventModerations.FirstOrDefaultAsync(m => m.Id == moderationId, ct);
        // Already decided (e.g. an Admin reviewed it manually before this job ran) or the row is
        // gone — nothing left to score.
        if (moderation is null || moderation.AdminDecision is not null) return;

        var content = await ResolveContentAsync(moderation, ct);
        if (content is null) return;

        var result = await _aiModeration.ScoreAsync(content, ct);
        if (result is null)
        {
            _logger.LogInformation(
                "AI moderation scoring unavailable for EventModerationId={ModerationId} — left unscored, still covered by SlaDeadline.",
                moderationId);
            return;
        }

        moderation.AiScore = result.Score;
        moderation.RiskLevel = Enum.TryParse<ModerationRiskLevel>(result.RiskLevel, true, out var risk) ? risk : null;
        moderation.FlagReason = result.FlagReason;
        moderation.AiRecommendation =
            Enum.TryParse<AiModerationRecommendation>(result.Recommendation, true, out var rec) ? rec : null;

        await _ctx.SaveChangesAsync(ct);
    }

    private async Task<string?> ResolveContentAsync(EventModeration moderation, CancellationToken ct)
    {
        LoungeShow? show;
        if (moderation.TargetType == ModerationTargetType.Show)
        {
            show = await _ctx.LoungeShows.FirstOrDefaultAsync(s => s.Id == moderation.TargetId, ct);
        }
        else if (moderation.TargetType == ModerationTargetType.Livestream)
        {
            var livestream = await _ctx.Livestreams.FirstOrDefaultAsync(l => l.Id == moderation.TargetId, ct);
            show = livestream is null
                ? null
                : await _ctx.LoungeShows.FirstOrDefaultAsync(s => s.Id == livestream.LoungeShowId, ct);
        }
        else
        {
            show = null;
        }

        return show is null ? null : $"Tên show: {show.Name}\nMô tả: {show.Description}";
    }
}
