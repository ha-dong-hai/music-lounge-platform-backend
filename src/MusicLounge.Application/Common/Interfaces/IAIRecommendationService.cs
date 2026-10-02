namespace MusicLounge.Application.Common.Interfaces;

public interface IAIRecommendationService
{
    Task TriggerRecommendationRefreshAsync(Guid userId, CancellationToken ct = default);
}
