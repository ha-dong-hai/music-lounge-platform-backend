using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using MusicLounge.Application.Common;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Exceptions;
using MusicLounge.Infrastructure.Settings;

namespace MusicLounge.Infrastructure.Services;

/// <summary>
/// MLACP-480: tạo ảnh nền poster bằng Gemini image API.
///
/// <para>Chỉ chạy khi <c>Gemini:ImageModel</c> được đặt — lý do ở <see cref="GeminiSettings.ImageModel"/>: khoá Gemini
/// dùng chung với kiểm duyệt nội dung, mà kiểm duyệt chạy được trên bậc miễn phí còn sinh ảnh thì không.</para>
///
/// <para>Dùng endpoint <c>/v1beta/interactions</c>, KHÔNG phải <c>generateContent</c>. Bản
/// <c>GeminiImageGenerationService</c> cũ (xoá tháng 8/2026) viết theo kiểu cũ nên không dùng lại được.</para>
///
/// <para>Ảnh ra chỉ mang SynthID — dấu vô hình. Khác với đường Google Flow: Flow in một ngôi sao 4 cánh NHÌN THẤY
/// ĐƯỢC lên ảnh (đo thật 19/09/2026), nên với poster thương mại thì đường này sạch hơn.</para>
/// </summary>
public sealed class GeminiImageGenerationService : IAiImageGenerationService
{
    public string ProviderName => "gemini";

    private readonly IHttpClientFactory _httpFactory;
    private readonly GeminiSettings _settings;

    public GeminiImageGenerationService(IHttpClientFactory httpFactory, IOptions<GeminiSettings> settings)
    {
        _httpFactory = httpFactory;
        _settings = settings.Value;
    }

    public async Task<byte[]> GenerateImageAsync(string prompt, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_settings.ApiKey) || string.IsNullOrWhiteSpace(_settings.ImageModel))
            throw new ExternalServiceException("Gemini", "Chưa cấu hình Gemini:ApiKey/ImageModel.");

        var http = _httpFactory.CreateClient("gemini");

        using var request = new HttpRequestMessage(
            HttpMethod.Post, "https://generativelanguage.googleapis.com/v1beta/interactions");
        request.Headers.Add("x-goog-api-key", _settings.ApiKey);
        request.Content = JsonContent.Create(new
        {
            model = _settings.ImageModel,
            input = new[] { new { type = "text", text = prompt } },
            response_format = new
            {
                type = "image",
                mime_type = "image/jpeg",
                // Dùng CHUNG hằng số với hàng đợi máy trạm: hai đường sinh ảnh phải ra cùng khổ, nếu không thì lớp in
                // chữ bằng font sẽ đặt tiêu đề lệch chỗ tuỳ theo hôm đó nhà cung cấp nào chạy.
                aspect_ratio = PosterQueue.AspectRatio
            }
        });

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
            throw new ExternalServiceException(
                "Gemini", $"{(int)response.StatusCode} {response.StatusCode}: {errorBody}");
        }

        var payload = await response.Content.ReadFromJsonAsync<GeminiInteraction>(cancellationToken: ct)
            ?? throw new ExternalServiceException("Gemini", "Response body was empty or invalid.");

        var b64 = TimAnh(payload)
            ?? throw new ExternalServiceException("Gemini", "Response contained no image data.");

        try
        {
            return Convert.FromBase64String(b64);
        }
        catch (FormatException ex)
        {
            throw new ExternalServiceException("Gemini", "Response image was not valid base64.", ex);
        }
    }

    /// <summary>
    /// Lấy ảnh theo ĐÚNG CẤU TRÚC, không đi tìm "chuỗi base64 dài nhất".
    ///
    /// <para>Đây là bẫy thật, đo được 21/09/2026: phản hồi có HAI khối base64 — bước <c>type: "thought"</c> mang
    /// trường <c>signature</c> dài 1.064.956 ký tự, còn ảnh thật nằm ở bước <c>type: "model_output"</c> và chỉ
    /// 643.948 ký tự. Bộ đọc nào chọn chuỗi dài nhất sẽ ghi ra tệp hỏng, và hỏng theo kiểu tệ nhất: có tệp, đúng
    /// phần mở rộng, mở ra mới biết.</para>
    /// </summary>
    private static string? TimAnh(GeminiInteraction payload)
    {
        foreach (var step in payload.Steps ?? [])
        {
            if (step.Type != "model_output") continue;
            foreach (var noiDung in step.Content ?? [])
                if (noiDung.Type == "image" && !string.IsNullOrEmpty(noiDung.Data))
                    return noiDung.Data;
        }

        return null;
    }

    private sealed record GeminiInteraction(
        [property: JsonPropertyName("status")] string? Status,
        [property: JsonPropertyName("steps")] IReadOnlyList<GeminiStep>? Steps);

    private sealed record GeminiStep(
        [property: JsonPropertyName("type")] string? Type,
        [property: JsonPropertyName("content")] IReadOnlyList<GeminiContent>? Content);

    private sealed record GeminiContent(
        [property: JsonPropertyName("type")] string? Type,
        [property: JsonPropertyName("mime_type")] string? MimeType,
        [property: JsonPropertyName("data")] string? Data);
}
