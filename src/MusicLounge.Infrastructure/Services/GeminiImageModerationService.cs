using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Infrastructure.Settings;

namespace MusicLounge.Infrastructure.Services;

// Mirrors GeminiModerationService (text) exactly, but for image content — same model/API key
// (Gemini's generateContent endpoint accepts an inline_data image part alongside the text prompt
// in one request, no separate vision-specific endpoint needed).
public sealed class GeminiImageModerationService : IImageModerationService
{
    private static readonly JsonSerializerOptions CaseInsensitive = new() { PropertyNameCaseInsensitive = true };

    private const string PromptTemplate = """
        Bạn là hệ thống kiểm duyệt ảnh cho một nền tảng đặt vé nhạc sống (phòng trà) tại Việt Nam.
        Ảnh này sẽ được hiển thị công khai làm ảnh gallery hoặc tour ảo 360° của một phòng trà.
        Đánh giá ảnh có dấu hiệu vi phạm chính sách không (khoả thân/nội dung khiêu dâm, bạo lực,
        chất cấm, biểu tượng thù ghét, hoặc nội dung không liên quan/không phù hợp với không gian
        kinh doanh phòng trà).

        Trả lời DUY NHẤT một JSON object đúng theo format sau, không thêm chữ nào khác, không dùng markdown:
        {"score": <số thập phân 0.0 (an toàn) đến 1.0 (rất đáng ngờ)>, "riskLevel": "<Low|Medium|High|Critical>", "flagReason": "<lý do ngắn gọn nếu có vấn đề, hoặc null>", "recommendation": "<SuggestApprove|NeedsReview|SuggestReject>"}
        """;

    private readonly IHttpClientFactory _httpFactory;
    private readonly GeminiSettings _settings;
    private readonly ILogger<GeminiImageModerationService> _logger;

    public GeminiImageModerationService(
        IHttpClientFactory httpFactory, IOptions<GeminiSettings> settings, ILogger<GeminiImageModerationService> logger)
    {
        _httpFactory = httpFactory;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<AiModerationResult?> CheckAsync(byte[] imageBytes, string mimeType, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_settings.ApiKey))
            return null;

        var http = _httpFactory.CreateClient("gemini");

        var requestBody = new
        {
            contents = new[]
            {
                new
                {
                    parts = new object[]
                    {
                        new { text = PromptTemplate },
                        new { inline_data = new { mime_type = mimeType, data = Convert.ToBase64String(imageBytes) } }
                    }
                }
            },
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
                    "Gemini image moderation call failed: {Status} {Body} — leaving image unscored.",
                    response.StatusCode, errorBody);
                return null;
            }

            var payload = await response.Content.ReadFromJsonAsync<GeminiResponse>(cancellationToken: ct);
            var text = payload?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text;
            if (string.IsNullOrWhiteSpace(text))
            {
                _logger.LogWarning("Gemini image moderation response had no text content — leaving image unscored.");
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
            _logger.LogWarning(ex, "Gemini image moderation scoring threw — leaving image unscored (fail-open).");
            return null;
        }
    }

    private sealed record GeminiResponse(GeminiCandidate[]? Candidates);
    private sealed record GeminiCandidate(GeminiContent? Content);
    private sealed record GeminiContent(GeminiPart[]? Parts);
    private sealed record GeminiPart(string? Text);
    private sealed record GeminiModerationJson(float Score, string RiskLevel, string? FlagReason, string Recommendation);
}
