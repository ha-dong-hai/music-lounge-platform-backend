using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Exceptions;
using MusicLounge.Infrastructure.Settings;

namespace MusicLounge.Infrastructure.Services;

public sealed class CloudflareStreamService : ILivestreamService
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly CloudflareSettings _settings;

    public CloudflareStreamService(IHttpClientFactory httpFactory, IOptions<CloudflareSettings> settings)
    {
        _httpFactory = httpFactory;
        _settings = settings.Value;
    }

    public async Task<LivestreamProviderResult> CreateStreamAsync(string name, CancellationToken ct = default)
    {
        var http = _httpFactory.CreateClient("cloudflare");
        var url = $"https://api.cloudflare.com/client/v4/accounts/{_settings.AccountId}/stream/live_inputs";

        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.ApiToken);
        request.Content = JsonContent.Create(new { meta = new { name } });

        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            if (ex is TaskCanceledException && ct.IsCancellationRequested) throw;
            throw new ExternalServiceException("Cloudflare Stream", ex.Message, ex);
        }

        var result = await response.Content.ReadFromJsonAsync<CloudflareApiResponse>(cancellationToken: ct)
            ?? throw new ExternalServiceException("Cloudflare Stream", "Response body was empty or invalid.");

        return new LivestreamProviderResult(
            result.Result.Uid,
            result.Result.Rtmps.Url,
            result.Result.Rtmps.StreamKey,
            result.Result.Playback.Hls);
    }

    public async Task DeleteStreamAsync(string providerRef, CancellationToken ct = default)
    {
        var http = _httpFactory.CreateClient("cloudflare");
        var url = $"https://api.cloudflare.com/client/v4/accounts/{_settings.AccountId}/stream/live_inputs/{providerRef}";

        using var request = new HttpRequestMessage(HttpMethod.Delete, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.ApiToken);

        try
        {
            var response = await http.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            if (ex is TaskCanceledException && ct.IsCancellationRequested) throw;
            throw new ExternalServiceException("Cloudflare Stream", ex.Message, ex);
        }
    }

    private sealed record CloudflareApiResponse(CloudflareResult Result);
    private sealed record CloudflareResult(string Uid, CloudflareRtmps Rtmps, CloudflarePlayback Playback);
    private sealed record CloudflareRtmps(string Url, string StreamKey);
    private sealed record CloudflarePlayback(string Hls);
}
