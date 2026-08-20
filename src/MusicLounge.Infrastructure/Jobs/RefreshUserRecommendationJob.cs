using Hangfire;
using MusicLounge.Application.Common.Interfaces;

namespace MusicLounge.Infrastructure.Jobs;

/// <summary>
/// Ad-hoc single-user recommendation refresh, enqueued from GetRecommendedLoungeShowsQueryHandler
/// on a cache miss (rather than making that user wait up to an hour for the next
/// RefreshRecommendationsJob run). Concurrency-guarded because IAIRecommendationService
/// (MLNetRecommendationService) retrains its collaborative-filtering model fresh per DI scope —
/// without this, several users' 6-hour recommendation-cache TTLs expiring around the same time
/// could trigger multiple full ALS retrains running concurrently against each other.
/// </summary>
public sealed class RefreshUserRecommendationJob
{
    private readonly IAIRecommendationService _aiService;

    public RefreshUserRecommendationJob(IAIRecommendationService aiService) => _aiService = aiService;

    [DisableConcurrentExecution(timeoutInSeconds: 30)]
    public Task ExecuteAsync(int userId, IJobCancellationToken cancellationToken)
        => _aiService.TriggerRecommendationRefreshAsync(userId, cancellationToken.ShutdownToken);
}
