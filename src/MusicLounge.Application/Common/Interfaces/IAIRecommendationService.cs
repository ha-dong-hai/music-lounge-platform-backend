namespace MusicLounge.Application.Common.Interfaces;

public interface IAIRecommendationService
{
    Task TriggerRecommendationRefreshAsync(int userId, CancellationToken ct = default);
}
