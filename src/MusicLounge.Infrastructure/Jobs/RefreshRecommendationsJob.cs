using Microsoft.EntityFrameworkCore;
using Hangfire;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Infrastructure.Persistence;

namespace MusicLounge.Infrastructure.Jobs;

public sealed class RefreshRecommendationsJob
{
    private readonly ApplicationDbContext _ctx;
    private readonly IAIRecommendationService _aiService;

    public RefreshRecommendationsJob(ApplicationDbContext ctx, IAIRecommendationService aiService)
    {
        _ctx = ctx;
        _aiService = aiService;
    }

    [DisableConcurrentExecution(timeoutInSeconds: 30)]
    public async Task ExecuteAsync(IJobCancellationToken cancellationToken)
    {
        var ct = cancellationToken.ShutdownToken;
        var userIds = await _ctx.Users
            .AsNoTracking()
            .Where(u => u.AiConsent)
            .Select(u => u.Id)
            .ToListAsync(ct);

        foreach (var userId in userIds)
            await _aiService.TriggerRecommendationRefreshAsync(userId, ct);
    }
}
