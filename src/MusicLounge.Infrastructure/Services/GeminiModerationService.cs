using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Infrastructure.Settings;

namespace MusicLounge.Infrastructure.Services;

public sealed class GeminiModerationService : IAiModerationService
{
    private static readonly JsonSerializerOptions CaseInsensitive = new() { PropertyNameCaseInsensitive = true };

    private const string PromptTemplate = """
        Bạn là hệ thống kiểm duyệt nội dung cho một nền tảng đặt vé nhạc sống (phòng trà) tại Việt Nam.
        Đánh giá nội dung sự kiện dưới đây có dấu hiệu vi phạm chính sách không (nội dung phản cảm,
        lừa đảo, spam, ngôn từ thù ghét, quảng cáo trá hình, hoặc thông tin sai sự thật).

        Trả lời DUY NHẤT một JSON object đúng theo format sau, không thêm chữ nào khác, không dùng markdown:
        {"score": <số thập phân 0.0 (an toàn) đến 1.0 (rất đáng ngờ)>, "riskLevel": "<Low|Medium|High|Critical>", "flagReason": "<lý do ngắn gọn nếu có vấn đề, hoặc null>", "recommendation": "<SuggestApprove|NeedsReview|SuggestReject>"}

        Nội dung cần đánh giá:
        {{CONTENT}}
        """;

    private readonly IHttpClientFactory _httpFactory;
    private readonly GeminiSettings _settings;
    private readonly ILogger<GeminiModerationService> _logger;

    public GeminiModerationService(
        IHttpClientFactory httpFactory, IOptions<GeminiSettings> settings, ILogger<GeminiModerationService> logger)
    {
        _httpFactory = httpFactory;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<AiModerationResult?> ScoreAsync(string content, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_settings.ApiKey))
            return null;

        var http = _httpFactory.CreateClient("gemini");
        var prompt = PromptTemplate.Replace("{{CONTENT}}", content);

        var requestBody = new
        {
            contents = new[] { new { parts = new[] { new { text = prompt } } } },
            generationConfig = new { responseMimeType = "application/json" }
        };

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"https://generativelanguage.googleapis.com/v1beta/models/{_settings.Model}:generateContent");
        request.Headers.Add("x-goog-api-key", _settings.ApiKey);
        request.Content = JsonContent.Create(requestBody);

        try
        {
            var response = await http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning(
                    "Gemini moderation call failed: {Status} {Body} — leaving content unscored.",
                    response.StatusCode, errorBody);
                return null;
            }

            var payload = await response.Content.ReadFromJsonAsync<GeminiResponse>(cancellationToken: ct);
            var text = payload?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text;
            if (string.IsNullOrWhiteSpace(text))
            {
                _logger.LogWarning("Gemini moderation response had no text content — leaving content unscored.");
                return null;
            }

            var parsed = JsonSerializer.Deserialize<GeminiModerationJson>(text, CaseInsensitive);
            if (parsed is null) return null;

            return new AiModerationResult(
                Math.Clamp(parsed.Score, 0f, 1f), parsed.RiskLevel, parsed.FlagReason, parsed.Recommendation);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            if (ex is TaskCanceledException && ct.IsCancellationRequested) throw;
            _logger.LogWarning(ex, "Gemini moderation scoring threw — leaving content unscored (fail-open).");
            return null;
        }
    }

    private sealed record GeminiResponse(GeminiCandidate[]? Candidates);
    private sealed record GeminiCandidate(GeminiContent? Content);
    private sealed record GeminiContent(GeminiPart[]? Parts);
    private sealed record GeminiPart(string? Text);
    private sealed record GeminiModerationJson(float Score, string RiskLevel, string? FlagReason, string Recommendation);
}
