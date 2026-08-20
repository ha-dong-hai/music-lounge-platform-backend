using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Exceptions;
using MusicLounge.Infrastructure.Settings;

namespace MusicLounge.Infrastructure.Services;

public sealed class OpenAiImageGenerationService : IAiImageGenerationService
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly OpenAiSettings _settings;

    public OpenAiImageGenerationService(IHttpClientFactory httpFactory, IOptions<OpenAiSettings> settings)
    {
        _httpFactory = httpFactory;
        _settings = settings.Value;
    }

    public async Task<byte[]> GenerateImageAsync(string prompt, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_settings.ApiKey))
            throw new ExternalServiceException("OpenAI", "Chưa cấu hình OpenAI API key.");

        var http = _httpFactory.CreateClient("openai");
        var requestBody = new
        {
            model = _settings.Model,
            prompt,
            size = "1024x1024",
            // "medium" balances cost against looking professional enough to actually post — "low"
            // renders noticeably rougher, "high" is ~4x the cost of medium for a marketing image
            // that's realistically viewed on a phone screen (Facebook/Zalo), not printed.
            quality = "medium",
            n = 1
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/images/generations");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.ApiKey);
        request.Content = JsonContent.Create(requestBody);

        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            if (ex is TaskCanceledException && ct.IsCancellationRequested) throw;
            throw new ExternalServiceException("OpenAI", ex.Message, ex);
        }

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            throw new ExternalServiceException("OpenAI", $"{(int)response.StatusCode} {response.StatusCode}: {errorBody}");
        }

        var payload = await response.Content.ReadFromJsonAsync<OpenAiImageResponse>(cancellationToken: ct)
            ?? throw new ExternalServiceException("OpenAI", "Response body was empty or invalid.");

        var b64 = payload.Data?.FirstOrDefault()?.B64Json;
        if (string.IsNullOrEmpty(b64))
            throw new ExternalServiceException("OpenAI", "Response contained no image data.");

        return Convert.FromBase64String(b64);
    }

    private sealed record OpenAiImageResponse(OpenAiImageData[]? Data);
    private sealed record OpenAiImageData([property: JsonPropertyName("b64_json")] string? B64Json);
}
