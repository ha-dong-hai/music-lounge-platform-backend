using MusicLounge.Application.Common.Interfaces;

namespace MusicLounge.Tests.Integration.Fakes;

public sealed class FakeAiService : IAIRecommendationService
{
    public Task TriggerRecommendationRefreshAsync(int userId, CancellationToken ct = default)
        => Task.CompletedTask;
}

public sealed class FakeFcmService : IFcmService
{
    public Task SendAsync(int userId, string title, string body, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task SendAsync(int userId, string title, string body,
        Dictionary<string, string> data, CancellationToken ct = default)
        => Task.CompletedTask;
}
