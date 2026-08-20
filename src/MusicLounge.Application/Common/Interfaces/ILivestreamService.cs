namespace MusicLounge.Application.Common.Interfaces;

public interface ILivestreamService
{
    Task<LivestreamProviderResult> CreateStreamAsync(string name, CancellationToken ct = default);
    Task DeleteStreamAsync(string providerRef, CancellationToken ct = default);
}

public sealed record LivestreamProviderResult(
    string ProviderRef,
    string RtmpUrl,
    string StreamKey,
    string HlsUrl);
