using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Infrastructure.Settings;

namespace MusicLounge.Infrastructure.Services;

public sealed class GeminiTextGenerationService : IAiTextGenerationService
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly GeminiSettings _settings;
    private readonly ILogger<GeminiTextGenerationService> _logger;

    public GeminiTextGenerationService(
        IHttpClientFactory httpFactory, IOptions<GeminiSettings> settings, ILogger<GeminiTextGenerationService> logger)
    {
        _httpFactory = httpFactory;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<string?> GenerateJsonAsync(string prompt, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_settings.ApiKey))
            return null;

        var http = _httpFactory.CreateClient("gemini");
        var requestBody = new
        {
            contents = new[] { new { parts = new[] { new { text = prompt } } } },
            generationConfig = new { responseMimeType = "application/json" }
        };

        using var request = new HttpRequestMessage(
            HttpMethod.Post, $"https://generativelanguage.googleapis.com/v1beta/models/{_settings.Model}:generateContent");
        request.Headers.Add("x-goog-api-key", _settings.ApiKey);
        request.Content = JsonContent.Create(requestBody);

        try
        {
            var response = await http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("Gemini text generation call failed: {Status} {Body}", response.StatusCode, errorBody);
                return null;
            }

            var payload = await response.Content.ReadFromJsonAsync<GeminiResponse>(cancellationToken: ct);
            return payload?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            if (ex is TaskCanceledException && ct.IsCancellationRequested) throw;
            _logger.LogWarning(ex, "Gemini text generation threw — caller keeps its non-AI fallback.");
            return null;
        }
    }

    private sealed record GeminiResponse(GeminiCandidate[]? Candidates);
    private sealed record GeminiCandidate(GeminiContent? Content);
    private sealed record GeminiContent(GeminiPart[]? Parts);
    private sealed record GeminiPart(string? Text);
}
