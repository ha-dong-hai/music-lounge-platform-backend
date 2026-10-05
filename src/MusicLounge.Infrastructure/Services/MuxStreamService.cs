using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Exceptions;
using MusicLounge.Infrastructure.Settings;

namespace MusicLounge.Infrastructure.Services;

public sealed class MuxStreamService : ILivestreamService
{
    private const string RtmpIngestUrl = "rtmps://global-live.mux.com:443/app";

    private readonly IHttpClientFactory _httpFactory;
    private readonly MuxSettings _settings;

    public MuxStreamService(IHttpClientFactory httpFactory, IOptions<MuxSettings> settings)
    {
        _httpFactory = httpFactory;
        _settings = settings.Value;
    }

    public Task<LivestreamProviderResult> CreateStreamAsync(string name, CancellationToken ct = default)
        => CreateStreamAsync(name, paidViewing: false, ct);

    /// <summary>
    /// MLACP-647. Buổi phát CÓ PHÍ được tạo với playback policy "signed" khi đã cấu hình khoá ký (Mux:SigningKeyId +
    /// Mux:SigningKeyPrivate). Trước đây mọi luồng đều "public": người có vé mở công cụ trình duyệt là chép được
    /// https://stream.mux.com/{id}.m3u8, gửi cho ai cũng xem được, và giới hạn 2 thiết bị / vé chỉ chặn trên web.
    /// <para>Chưa cấu hình khoá thì vẫn "public" như cũ — cấu hình là việc vận hành (tạo signing key trên Mux Dashboard).
    /// Luồng "signed" được đánh dấu bằng <see cref="SignedMarker"/> ngay trên HlsUrl lưu trong DB: không cần cột mới, và
    /// một HlsUrl bị lộ từ DB cũng không phát được vì thiếu token.</para>
    /// </summary>
    public async Task<LivestreamProviderResult> CreateStreamAsync(string name, bool paidViewing, CancellationToken ct = default)
    {
        var http = _httpFactory.CreateClient("mux");
        var signed = paidViewing && SigningConfigured;

        var body = new
        {
            playback_policy = new[] { signed ? "signed" : "public" },
            // MLACP-510: KHÔNG gửi new_asset_settings — trường đó bảo Mux ghi lại cả buổi phát thành một Asset (bản ghi
            // VOD, tính phí lưu trữ). Hệ thống không có xem lại (chủ dự án chốt 16/09, "bỏ hẳn" 01/10).
            passthrough = name
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.mux.com/video/v1/live-streams");
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", GetCredentials());
        request.Content = JsonContent.Create(body);

        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            if (ex is TaskCanceledException && ct.IsCancellationRequested) throw;
            throw new ExternalServiceException("Mux", ex.Message, ex);
        }

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            throw new ExternalServiceException("Mux", $"{(int)response.StatusCode} {response.StatusCode}: {errorBody}");
        }

        var result = await response.Content.ReadFromJsonAsync<MuxApiResponse>(cancellationToken: ct)
            ?? throw new ExternalServiceException("Mux", "Response body was empty or invalid.");

        if (result.Data.PlaybackIds is not { Length: > 0 })
            throw new ExternalServiceException("Mux", "Response contained no playback IDs.");

        var playbackId = result.Data.PlaybackIds[0].Id;

        return new LivestreamProviderResult(
            result.Data.Id,
            RtmpIngestUrl,
            result.Data.StreamKey,
            $"https://stream.mux.com/{playbackId}.m3u8" + (signed ? SignedMarker : ""));
    }

    /// <summary>Đánh dấu luồng tạo với playback policy "signed" trên HlsUrl lưu trong DB (xem CreateStreamAsync).</summary>
    public const string SignedMarker = "?signed=1";

    private bool SigningConfigured =>
        !string.IsNullOrWhiteSpace(_settings.SigningKeyId) && !string.IsNullOrWhiteSpace(_settings.SigningKeyPrivate);

    /// <summary>
    /// MLACP-647. Luồng có chữ ký: thay <see cref="SignedMarker"/> bằng token JWT RS256 theo tài liệu Mux "Secure video
    /// playback" (header kid = signing key id; claims sub = playback id, aud = "v", exp). Token có hạn nên link chép đi chỉ
    /// dùng được tới <paramref name="validUntil"/>. Luồng thường hoặc chưa cấu hình khoá: trả nguyên.
    /// <para>Trần giới hạn: token gắn với luồng, không gắn với người xem — trong hạn token, link chép vẫn phát được. Chặn
    /// triệt để cần DRM/watermark theo người xem — ngoài phạm vi.</para>
    /// </summary>
    public string ViewerPlaybackUrl(string storedHlsUrl, DateTimeOffset validUntil)
    {
        if (!storedHlsUrl.EndsWith(SignedMarker, StringComparison.Ordinal)) return storedHlsUrl;
        var baseUrl = storedHlsUrl[..^SignedMarker.Length];
        if (!SigningConfigured) return baseUrl; // khoá bị gỡ khỏi cấu hình: không có gì để ký — Mux sẽ từ chối, đúng như phải thế

        var playbackId = baseUrl[(baseUrl.LastIndexOf('/') + 1)..].Replace(".m3u8", "", StringComparison.Ordinal);
        return $"{baseUrl}?token={MuxPlaybackToken.Create(_settings.SigningKeyId, _settings.SigningKeyPrivate, playbackId, validUntil)}";
    }

    public async Task DeleteStreamAsync(string providerRef, CancellationToken ct = default)
    {
        var http = _httpFactory.CreateClient("mux");
        var url = $"https://api.mux.com/video/v1/live-streams/{providerRef}";

        try
        {
            // MLACP-512: TẮT trước rồi mới XOÁ. Mux trả 400 khi xoá một luồng còn 'active' (encoder/OBS vẫn đẩy tín hiệu) —
            // đo bằng Mux thật 01/10 (fe M-441/M-442). Ba nơi gọi hàm này (Kết thúc, Admin cắt sóng, xử lý báo cáo nội
            // dung) đều xoá kiểu best-effort chỉ ghi log, nên luồng còn active cứ thế chạy tiếp và bị Mux TÍNH TIỀN mà không
            // ai biết. Tắt (disable) ngắt encoder và đưa luồng về 'disabled'; sau đó xoá trả 204. Sửa ở đây là sửa cho cả
            // ba nơi gọi. 404 khi tắt nghĩa là luồng đã không còn — không còn gì tính tiền, coi như xong.
            using (var disable = new HttpRequestMessage(HttpMethod.Put, $"{url}/disable"))
            {
                disable.Headers.Authorization = new AuthenticationHeaderValue("Basic", GetCredentials());
                var response = await http.SendAsync(disable, ct);
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return;
                response.EnsureSuccessStatusCode();
            }

            using var delete = new HttpRequestMessage(HttpMethod.Delete, url);
            delete.Headers.Authorization = new AuthenticationHeaderValue("Basic", GetCredentials());
            (await http.SendAsync(delete, ct)).EnsureSuccessStatusCode();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            if (ex is TaskCanceledException && ct.IsCancellationRequested) throw;
            throw new ExternalServiceException("Mux", ex.Message, ex);
        }
    }

    private string GetCredentials()
        => Convert.ToBase64String(
            System.Text.Encoding.ASCII.GetBytes($"{_settings.TokenId}:{_settings.TokenSecret}"));

    private sealed record MuxApiResponse(MuxLiveStream Data);

    private sealed record MuxLiveStream(
        string Id,
        [property: JsonPropertyName("stream_key")] string StreamKey,
        [property: JsonPropertyName("playback_ids")] MuxPlaybackId[] PlaybackIds);

    private sealed record MuxPlaybackId(string Id);
}
