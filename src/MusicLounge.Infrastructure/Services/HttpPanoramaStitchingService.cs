using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Exceptions;
using MusicLounge.Infrastructure.Settings;

namespace MusicLounge.Infrastructure.Services;

public sealed class HttpPanoramaStitchingService : IPanoramaStitchingService
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly PanoramaStitcherSettings _settings;

    public HttpPanoramaStitchingService(IHttpClientFactory httpFactory, IOptions<PanoramaStitcherSettings> settings)
    {
        _httpFactory = httpFactory;
        _settings = settings.Value;
    }

    public async Task<byte[]> StitchAsync(IReadOnlyList<string> imageUrls, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_settings.BaseUrl))
            throw new ExternalServiceException("PanoramaStitcher", "Chưa cấu hình PanoramaStitcher:BaseUrl.");

        var http = _httpFactory.CreateClient("panorama-stitcher");

        HttpResponseMessage response;
        try
        {
            response = await http.PostAsJsonAsync(
                $"{_settings.BaseUrl.TrimEnd('/')}/stitch", new { image_urls = imageUrls }, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            if (ex is TaskCanceledException && ct.IsCancellationRequested) throw;
            throw new ExternalServiceException("PanoramaStitcher", ex.Message, ex);
        }

        if (!response.IsSuccessStatusCode)
        {
            // FastAPI's HTTPException serializes as {"detail": "..."} — surface that specific
            // reason (e.g. "not enough overlap between photos") rather than a bare status code,
            // since the caller is an Owner who needs to know what to actually fix.
            string reason;
            try
            {
                var errorBody = await response.Content.ReadFromJsonAsync<StitchErrorResponse>(cancellationToken: ct);
                reason = errorBody?.Detail ?? await response.Content.ReadAsStringAsync(ct);
            }
            catch
            {
                reason = await response.Content.ReadAsStringAsync(ct);
            }

            throw new ExternalServiceException("PanoramaStitcher", $"{(int)response.StatusCode}: {reason}");
        }

        return await response.Content.ReadAsByteArrayAsync(ct);
    }

    private sealed record StitchErrorResponse(string? Detail);
}
