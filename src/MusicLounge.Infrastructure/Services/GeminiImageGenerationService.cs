using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Exceptions;
using MusicLounge.Infrastructure.Settings;

namespace MusicLounge.Infrastructure.Services;

public sealed class GeminiImageGenerationService : IAiImageGenerationService
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly GeminiSettings _settings;

    public GeminiImageGenerationService(IHttpClientFactory httpFactory, IOptions<GeminiSettings> settings)
    {
        _httpFactory = httpFactory;
        _settings = settings.Value;
    }

    public async Task<byte[]> GenerateImageAsync(string prompt, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_settings.ApiKey))
            throw new ExternalServiceException("Gemini", "Chưa cấu hình Gemini API key.");

        var http = _httpFactory.CreateClient("gemini");
        var requestBody = new
        {
            contents = new[] { new { parts = new[] { new { text = prompt } } } }
        };

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"https://generativelanguage.googleapis.com/v1beta/models/{_settings.ImageModel}:generateContent");
        request.Headers.Add("x-goog-api-key", _settings.ApiKey);
        request.Content = JsonContent.Create(requestBody);

        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            if (ex is TaskCanceledException && ct.IsCancellationRequested) throw;
            throw new ExternalServiceException("Gemini", ex.Message, ex);
        }

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            throw new ExternalServiceException("Gemini", $"{(int)response.StatusCode} {response.StatusCode}: {errorBody}");
        }

        var payload = await response.Content.ReadFromJsonAsync<GeminiResponse>(cancellationToken: ct)
            ?? throw new ExternalServiceException("Gemini", "Response body was empty or invalid.");

        var inlineData = payload.Candidates?
            .SelectMany(c => c.Content?.Parts ?? [])
            .Select(p => p.InlineData)
            .FirstOrDefault(d => d?.Data is not null);

        if (inlineData?.Data is null)
            throw new ExternalServiceException("Gemini", "Response contained no image data.");

        return Convert.FromBase64String(inlineData.Data);
    }

    private sealed record GeminiResponse(GeminiCandidate[]? Candidates);
    private sealed record GeminiCandidate(GeminiContent? Content);
    private sealed record GeminiContent(GeminiPart[]? Parts);
    private sealed record GeminiPart(GeminiInlineData? InlineData);
    private sealed record GeminiInlineData(string? MimeType, string? Data);
}
