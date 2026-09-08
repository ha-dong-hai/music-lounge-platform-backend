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
        // StitchVenueTourSceneCommandValidator đã xác nhận mọi URL tới đây đều do chính hệ thống
        // này phát ra (IFileStorageService.IsOwnUploadUrl) — đó mới là thứ bịt lỗ SSRF, không phải
        // việc ghép chuỗi bên dưới.
        //
        // Hai dạng hợp lệ cần đối xử khác nhau: đường dẫn tương đối của kho cục bộ phải được nối
        // với PublicBaseUrl để dịch vụ Python tải được, còn URL kho đám mây thì đã tuyệt đối rồi và
        // nối thêm gì vào cũng chỉ làm hỏng nó.
        // PublicBaseUrl chỉ cần khi thật sự có đường dẫn tương đối phải nối — kho đám mây đã trả
        // URL tuyệt đối nên đòi nó vô điều kiện sẽ chặn tính năng vì một thiết lập không dùng tới.
        if (imageUrls.Any(u => u.StartsWith('/')) && string.IsNullOrWhiteSpace(_settings.PublicBaseUrl))
            throw new ExternalServiceException(
                "PanoramaStitcher",
                "Chưa cấu hình PanoramaStitcher:PublicBaseUrl (cần khi ảnh lưu trên đĩa cục bộ).");

        var absoluteUrls = imageUrls
            .Select(u => u.StartsWith('/') ? $"{_settings.PublicBaseUrl.TrimEnd('/')}{u}" : u)
            .ToArray();

        var http = _httpFactory.CreateClient("panorama-stitcher");

        HttpResponseMessage response;
        try
        {
            response = await http.PostAsJsonAsync(
                $"{_settings.BaseUrl.TrimEnd('/')}/stitch", new { image_urls = absoluteUrls }, ct);
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
