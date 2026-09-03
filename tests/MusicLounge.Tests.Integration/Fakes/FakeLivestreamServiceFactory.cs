using MusicLounge.Application.Common.Interfaces;

namespace MusicLounge.Tests.Integration.Fakes;

public sealed class FakeLivestreamService : ILivestreamService
{
    public Task<LivestreamProviderResult> CreateStreamAsync(string name, CancellationToken ct = default)
        => Task.FromResult(new LivestreamProviderResult(
            ProviderRef: $"fake-ref-{Guid.NewGuid():N}",
            RtmpUrl: "rtmp://live.fake.test/live",
            StreamKey: $"sk-{Guid.NewGuid():N}",
            HlsUrl: "https://fake.hls.test/stream.m3u8"));

    public Task DeleteStreamAsync(string providerRef, CancellationToken ct = default)
        => Task.CompletedTask;
}

public sealed class FakeLivestreamServiceFactory : ILivestreamServiceFactory
{
    private readonly FakeLivestreamService _service = new();

    public ILivestreamService GetProvider(string? providerKey = null) => _service;
    public string ActiveProviderKey => "fake";
}
