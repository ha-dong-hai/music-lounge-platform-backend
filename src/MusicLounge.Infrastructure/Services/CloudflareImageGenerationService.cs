using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Exceptions;
using MusicLounge.Infrastructure.Settings;

namespace MusicLounge.Infrastructure.Services;

/// <summary>
/// MLACP-418: tao anh poster bang Cloudflare Workers AI (model FLUX.1 schnell).
///
/// Vi sao khong dung OpenAI: OpenAI KHONG co bac mien phi, nen tren moi truong chua nap tien tinh nang nay luon hong —
/// dung tinh nang nam trong goi subscription ma chu phong tra da tra tien. Workers AI cho ~10.000 neuron/ngay mien phi,
/// FLUX.1 schnell ton ~43 neuron/anh (~230 anh/ngay) — du xa cho muc dung cua nen tang nay. Cloudflare cung da co san
/// trong cau hinh du an (dung cho livestream).
///
/// Khoa Gemini co the tao anh (Nano Banana) nhung bac mien phi chi ~50 request/ngay va dang duoc danh cho kiem duyet
/// noi dung — de rieng ra thi mot tinh nang chet khong keo tinh nang kia chet theo.
/// </summary>
public sealed class CloudflareImageGenerationService : IAiImageGenerationService
{
    /// <summary>Model mac dinh: nhanh (4 buoc), chat luong du dung cho poster xem tren dien thoai.</summary>
    public const string DefaultModel = "@cf/black-forest-labs/flux-1-schnell";

    /// <summary>MLACP-458: ghi vao nhat ky de sau con doi chieu anh nao do nha cung cap nao sinh ra.</summary>
    public string ProviderName => "cloudflare";

    private readonly IHttpClientFactory _httpFactory;
    private readonly CloudflareSettings _settings;

    public CloudflareImageGenerationService(IHttpClientFactory httpFactory, IOptions<CloudflareSettings> settings)
    {
        _httpFactory = httpFactory;
        _settings = settings.Value;
    }

    public async Task<byte[]> GenerateImageAsync(string prompt, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_settings.AccountId) || string.IsNullOrWhiteSpace(_settings.ApiToken))
            throw new ExternalServiceException("Cloudflare", "Chưa cấu hình Cloudflare:AccountId/ApiToken.");

        var model = string.IsNullOrWhiteSpace(_settings.ImageModel) ? DefaultModel : _settings.ImageModel;
        var http = _httpFactory.CreateClient("cloudflare");

        // Gioi han cua Cloudflare: prompt toi da 2048 ky tu, steps toi da 8. Cat prompt thay vi de API tra 400 —
        // prompt do BuildPromptAsync ghep tu ten show/mo ta nen co the dai hon 2048 khi Owner viet mo ta rat dai.
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"https://api.cloudflare.com/client/v4/accounts/{_settings.AccountId}/ai/run/{model}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.ApiToken);
        request.Content = JsonContent.Create(new
        {
            prompt = prompt.Length > 2048 ? prompt[..2048] : prompt,
            steps = 4
        });

        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            if (ex is TaskCanceledException && ct.IsCancellationRequested) throw;
            throw new ExternalServiceException("Cloudflare", ex.Message, ex);
        }

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            throw new ExternalServiceException(
                "Cloudflare", $"{(int)response.StatusCode} {response.StatusCode}: {errorBody}");
        }

        var payload = await response.Content.ReadFromJsonAsync<CloudflareAiResponse>(cancellationToken: ct)
            ?? throw new ExternalServiceException("Cloudflare", "Response body was empty or invalid.");

        var b64 = payload.Result?.Image;
        if (string.IsNullOrEmpty(b64))
            throw new ExternalServiceException("Cloudflare", "Response contained no image data.");

        try
        {
            return Convert.FromBase64String(b64);
        }
        catch (FormatException ex)
        {
            throw new ExternalServiceException("Cloudflare", "Response image was not valid base64.", ex);
        }
    }

    private sealed record CloudflareAiResponse(
        [property: JsonPropertyName("result")] CloudflareAiResult? Result,
        [property: JsonPropertyName("success")] bool Success);

    private sealed record CloudflareAiResult([property: JsonPropertyName("image")] string? Image);
}
