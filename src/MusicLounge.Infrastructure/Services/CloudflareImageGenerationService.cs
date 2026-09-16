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

    /// <summary>MLACP-423: HTTP client rieng cho viec tao anh — xem chu thich o DependencyInjection.</summary>
    public const string HttpClientName = "cloudflare-image";

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
        var http = _httpFactory.CreateClient(HttpClientName);

        // Gioi han cua Cloudflare: prompt toi da 2048 ky tu. Cat prompt thay vi de API tra 400 — prompt do
        // BuildPromptAsync ghep tu ten show/mo ta nen co the dai hon 2048 khi Owner viet mo ta rat dai.
        var promptGui = prompt.Length > 2048 ? prompt[..2048] : prompt;

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"https://api.cloudflare.com/client/v4/accounts/{_settings.AccountId}/ai/run/{model}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.ApiToken);
        request.Content = TaoNoiDungYeuCau(model, promptGui);

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

    /// <summary>
    /// MLACP-423: hai ho model, hai kieu gui khac nhau — day la yeu cau cua API Cloudflare chu khong phai lua chon.
    ///
    /// FLUX 2 (flux-2-dev, flux-2-klein-4b/9b) chi nhan multipart/form-data. Gui JSON cho chung bi tra ve
    /// "400 required properties at '/' are 'multipart'", nghia la doi sang FLUX 2 ma khong sua cho nay thi tinh nang
    /// hong ngay tu lan goi dau. Da doi chieu bang cach goi that API ngay 16/09: cung mot prompt, JSON tra 400,
    /// multipart tra ve anh JPEG 690-770 KB.
    ///
    /// Nhanh multipart chi gui moi prompt, dung y nguyen dang da goi thu that va duoc chap nhan. CHUA KIEM CHUNG
    /// FLUX 2 co nhan them "steps" hay khong — han muc mien phi trong ngay da can truoc khi thu duoc — nen khong
    /// gui them gi ngoai nhung gi da biet chac la chay duoc. Neu sau nay do duoc "steps" co tac dung thi dang
    /// xem xet, vi flux-2-dev tinh tien theo tung buoc.
    /// </summary>
    private static HttpContent TaoNoiDungYeuCau(string model, string prompt)
    {
        if (!model.Contains("flux-2", StringComparison.OrdinalIgnoreCase))
        {
            // steps toi da 8 voi FLUX.1 schnell; 4 la muc da chay tren Azure tu MLACP-418.
            return JsonContent.Create(new { prompt, steps = 4 });
        }

        // Dung DUNG dang da goi thu that va duoc Cloudflare chap nhan: phan than chi co mot header
        // Content-Disposition voi ten DAT TRONG NHAY KEP. MultipartFormDataContent cua .NET mac dinh ghi
        // name=prompt (khong nhay) va tu them Content-Type: text/plain cho tung phan — chua kiem chung duoc
        // Cloudflare co nhan hay khong, va mot lan bi tu choi la chu phong tra mat mot luot tao poster.
        var phanPrompt = new StringContent(prompt);
        phanPrompt.Headers.Remove("Content-Type");
        phanPrompt.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data")
        {
            // Ten bien co dinh, khong lay tu du lieu nguoi dung, nen khong the chen ky tu pha boundary.
            Name = "\"prompt\""
        };
        return new MultipartFormDataContent { phanPrompt };
    }

    private sealed record CloudflareAiResponse(
        [property: JsonPropertyName("result")] CloudflareAiResult? Result,
        [property: JsonPropertyName("success")] bool Success);

    private sealed record CloudflareAiResult([property: JsonPropertyName("image")] string? Image);
}
