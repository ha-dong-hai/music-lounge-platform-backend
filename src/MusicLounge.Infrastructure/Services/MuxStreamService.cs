using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Exceptions;
using MusicLounge.Infrastructure.Settings;

namespace MusicLounge.Infrastructure.Services;

public sealed class MuxStreamService : ILivestreamService
{
    private const string RtmpIngestUrl = "rtmps://global-live.mux.com:443/app";

    private readonly IHttpClientFactory _httpFactory;
    private readonly MuxSettings _settings;

    public MuxStreamService(IHttpClientFactory httpFactory, IOptions<MuxSettings> settings)
    {
        _httpFactory = httpFactory;
        _settings = settings.Value;
    }

    public async Task<LivestreamProviderResult> CreateStreamAsync(string name, CancellationToken ct = default)
    {
        var http = _httpFactory.CreateClient("mux");

        var body = new
        {
            playback_policy = new[] { "public" },
            new_asset_settings = new { playback_policy = new[] { "public" } },
            passthrough = name
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.mux.com/video/v1/live-streams");
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", GetCredentials());
        request.Content = JsonContent.Create(body);

        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            if (ex is TaskCanceledException && ct.IsCancellationRequested) throw;
            throw new ExternalServiceException("Mux", ex.Message, ex);
        }

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            throw new ExternalServiceException("Mux", $"{(int)response.StatusCode} {response.StatusCode}: {errorBody}");
        }

        var result = await response.Content.ReadFromJsonAsync<MuxApiResponse>(cancellationToken: ct)
            ?? throw new ExternalServiceException("Mux", "Response body was empty or invalid.");

        if (result.Data.PlaybackIds is not { Length: > 0 })
            throw new ExternalServiceException("Mux", "Response contained no playback IDs.");

        var playbackId = result.Data.PlaybackIds[0].Id;

        return new LivestreamProviderResult(
            result.Data.Id,
            RtmpIngestUrl,
            result.Data.StreamKey,
            $"https://stream.mux.com/{playbackId}.m3u8");
    }

    public async Task DeleteStreamAsync(string providerRef, CancellationToken ct = default)
    {
        var http = _httpFactory.CreateClient("mux");

        using var request = new HttpRequestMessage(HttpMethod.Delete,
            $"https://api.mux.com/video/v1/live-streams/{providerRef}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", GetCredentials());

        try
        {
            var response = await http.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            if (ex is TaskCanceledException && ct.IsCancellationRequested) throw;
            throw new ExternalServiceException("Mux", ex.Message, ex);
        }
    }

    private string GetCredentials()
        => Convert.ToBase64String(
            System.Text.Encoding.ASCII.GetBytes($"{_settings.TokenId}:{_settings.TokenSecret}"));

    private sealed record MuxApiResponse(MuxLiveStream Data);

    private sealed record MuxLiveStream(
        string Id,
        [property: JsonPropertyName("stream_key")] string StreamKey,
        [property: JsonPropertyName("playback_ids")] MuxPlaybackId[] PlaybackIds);

    private sealed record MuxPlaybackId(string Id);
}
