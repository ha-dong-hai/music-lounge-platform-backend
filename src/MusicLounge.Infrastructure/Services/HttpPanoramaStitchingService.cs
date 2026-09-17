using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Exceptions;
using MusicLounge.Infrastructure.Settings;

namespace MusicLounge.Infrastructure.Services;

public sealed class HttpPanoramaStitchingService : IPanoramaStitchingService
{
    private const string TenDichVu = "PanoramaStitcher";

    // MLACP-432: chu phong tra doc truc tiep loi cua lan ghep. Truoc day ho thay chuoi ky thuat nhu
    // "[PanoramaStitcher] Chưa cấu hình PanoramaStitcher:BaseUrl." hay "401: Thiếu hoặc sai khoá xác thực." — khong lam
    // gi duoc voi no. Chi tiet ky thuat van day du trong log cho nguoi van hanh.
    internal const string ThongBaoSuCo =
        "Dịch vụ ghép ảnh đang gặp sự cố nên lượt ghép này chưa thành công. Vui lòng thử lại sau ít phút; nếu vẫn lỗi, "
        + "hãy liên hệ hỗ trợ.";

    internal const string ThongBaoQuaLau =
        "Bộ ảnh này ghép quá lâu nên hệ thống đã dừng. Hãy thử lại với ít ảnh hơn.";

    // Dich vu chay tren Azure Container Apps va tu tat khi khong dung (scale to zero) de khong ton phi. Lan goi dau sau
    // khi tat phai cho container khoi dong. Ingress cua Container Apps giu yeu cau trong luc cho, nhung toi da 240 giay
    // cho MOT yeu cau — gop chung thoi gian khoi dong voi thoi gian ghep (vai chuc giay tren CPU) vao mot lan POST
    // /stitch la de cham tran. Nen danh thuc bang GET /health truoc: nhe, khong can khoa, khong tai anh nao.
    // 3 phut la muc rong rai CHUA do tren Azure — do that o buoc trien khai roi chinh lai neu can.
    private static readonly TimeSpan ChoKhoiDongMacDinh = TimeSpan.FromMinutes(3);
    private static readonly TimeSpan GianCachThuLaiMacDinh = TimeSpan.FromSeconds(5);

    private readonly IHttpClientFactory _httpFactory;
    private readonly PanoramaStitcherSettings _settings;
    private readonly ILogger<HttpPanoramaStitchingService> _logger;
    private readonly TimeSpan _choKhoiDong;
    private readonly TimeSpan _gianCachThuLai;

    public HttpPanoramaStitchingService(
        IHttpClientFactory httpFactory, IOptions<PanoramaStitcherSettings> settings,
        ILogger<HttpPanoramaStitchingService> logger)
        : this(httpFactory, settings, logger, ChoKhoiDongMacDinh, GianCachThuLaiMacDinh)
    {
    }

    // Test rut ngan thoi gian cho. DI chi dung constructor public o tren.
    internal HttpPanoramaStitchingService(
        IHttpClientFactory httpFactory, IOptions<PanoramaStitcherSettings> settings,
        ILogger<HttpPanoramaStitchingService> logger, TimeSpan choKhoiDong, TimeSpan gianCachThuLai)
    {
        _httpFactory = httpFactory;
        _settings = settings.Value;
        _logger = logger;
        _choKhoiDong = choKhoiDong;
        _gianCachThuLai = gianCachThuLai;
    }

    public bool IsConfiguredFor(IReadOnlyList<string> imageUrls)
    {
        var thieu = CauHinhThieu(imageUrls);
        if (thieu is null) return true;

        _logger.LogError(
            "Từ chối ghép ảnh 360 vì chưa cấu hình {Setting} — chủ phòng trà không dùng được tính năng này.", thieu);
        return false;
    }

    public async Task<byte[]> StitchAsync(IReadOnlyList<string> imageUrls, CancellationToken ct = default)
    {
        // Handler da kiem cau hinh truoc khi tao luot thu; kiem lai o day vi cau hinh co the doi trong luc job nam cho.
        var thieu = CauHinhThieu(imageUrls);
        if (thieu is not null)
            throw BaoLoiHeThong(ThongBaoSuCo, $"Chưa cấu hình {thieu}.", null);

        // StitchVenueTourSceneCommandValidator đã xác nhận mọi URL tới đây đều do chính hệ thống
        // này phát ra (IFileStorageService.IsOwnUploadUrl) — đó mới là thứ bịt lỗ SSRF, không phải
        // việc ghép chuỗi bên dưới.
        //
        // Hai dạng hợp lệ cần đối xử khác nhau: đường dẫn tương đối của kho cục bộ phải được nối
        // với PublicBaseUrl để dịch vụ Python tải được, còn URL kho đám mây thì đã tuyệt đối rồi và
        // nối thêm gì vào cũng chỉ làm hỏng nó.
        var absoluteUrls = imageUrls
            .Select(u => u.StartsWith('/') ? $"{_settings.PublicBaseUrl.TrimEnd('/')}{u}" : u)
            .ToArray();

        var http = _httpFactory.CreateClient("panorama-stitcher");
        var goc = _settings.BaseUrl.TrimEnd('/');

        await DanhThucAsync(http, goc, ct);

        HttpResponseMessage response;
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{goc}/stitch")
            {
                Content = JsonContent.Create(new { image_urls = absoluteUrls })
            };
            // MLACP-431: dich vu ghep anh bat buoc khoa.
            request.Headers.Add("X-Stitcher-Key", _settings.ApiKey);
            response = await http.SendAsync(request, ct);
        }
        catch (OperationCanceledException ex) when (!ct.IsCancellationRequested)
        {
            // Het thoi gian cho cua HttpClient (120 giay). Dich vu da thuc day o buoc tren nen day la do ghep lau.
            throw BaoLoiHeThong(ThongBaoQuaLau, "Gọi /stitch quá thời gian chờ của HttpClient.", ex);
        }
        catch (HttpRequestException ex)
        {
            throw BaoLoiHeThong(ThongBaoSuCo, $"Không gọi được /stitch: {ex.Message}", ex);
        }

        using (response)
        {
            if (response.IsSuccessStatusCode)
                return await response.Content.ReadAsByteArrayAsync(ct);

            var tho = await response.Content.ReadAsStringAsync(ct);
            var lyDo = DocDetailChuoi(tho);

            // 422 la loi cua CHINH bo anh (anh khong du phan chong lan, OpenCV bao phoi sang lech...) va dich vu da
            // viet san loi huong dan chup lai — chu phong tra sua duoc, nen giu nguyen. Moi ma khac (401/503 thieu cau
            // hinh, 400 nguon anh sai hay khong tai duoc anh, 500...) la loi phia he thong, chu phong tra khong lam gi
            // duoc. Detail phai la CHUOI: FastAPI cung tra 422 cho body sai dinh dang, nhung khi do detail la mang loi
            // ky thuat.
            if (response.StatusCode == HttpStatusCode.UnprocessableEntity && lyDo is not null)
            {
                _logger.LogInformation("Bộ ảnh không ghép được: {Reason}", lyDo);
                throw new ExternalServiceException(TenDichVu, lyDo);
            }

            throw BaoLoiHeThong(
                ThongBaoSuCo, $"/stitch trả HTTP {(int)response.StatusCode}: {lyDo ?? CatNgan(tho)}", null);
        }
    }

    private string? CauHinhThieu(IReadOnlyList<string> imageUrls)
    {
        if (string.IsNullOrWhiteSpace(_settings.BaseUrl)) return "PanoramaStitcher:BaseUrl";
        if (string.IsNullOrWhiteSpace(_settings.ApiKey)) return "PanoramaStitcher:ApiKey";
        // PublicBaseUrl chỉ cần khi thật sự có đường dẫn tương đối phải nối — kho đám mây đã trả
        // URL tuyệt đối nên đòi nó vô điều kiện sẽ chặn tính năng vì một thiết lập không dùng tới.
        if (imageUrls.Any(u => u.StartsWith('/')) && string.IsNullOrWhiteSpace(_settings.PublicBaseUrl))
            return "PanoramaStitcher:PublicBaseUrl (cần khi ảnh lưu trên đĩa cục bộ)";
        return null;
    }

    /// <summary>
    /// Goi GET /health toi khi dich vu tra 2xx hoac het _choKhoiDong. Thu lai vi trong luc khoi dong ingress co the tra
    /// 502/503 thay vi giu yeu cau, va mot lan goi co the cham het thoi gian cho 120 giay cua HttpClient.
    /// </summary>
    private async Task DanhThucAsync(HttpClient http, string goc, CancellationToken ct)
    {
        var dongHo = Stopwatch.StartNew();
        var soLan = 0;
        var loiCuoi = "(chưa gọi lần nào)";

        while (dongHo.Elapsed < _choKhoiDong)
        {
            soLan++;
            using (var hetGio = CancellationTokenSource.CreateLinkedTokenSource(ct))
            {
                var conLai = _choKhoiDong - dongHo.Elapsed;
                hetGio.CancelAfter(conLai > TimeSpan.Zero ? conLai : TimeSpan.FromMilliseconds(1));
                try
                {
                    using var res = await http.GetAsync($"{goc}/health", hetGio.Token);
                    if (res.IsSuccessStatusCode)
                    {
                        if (soLan > 1)
                            _logger.LogInformation(
                                "Dịch vụ ghép ảnh sẵn sàng sau {Seconds:0.0}s ({Attempts} lần gọi /health)",
                                dongHo.Elapsed.TotalSeconds, soLan);
                        return;
                    }
                    loiCuoi = $"HTTP {(int)res.StatusCode}";
                }
                catch (Exception ex) when ((ex is HttpRequestException or OperationCanceledException)
                                           && !ct.IsCancellationRequested)
                {
                    loiCuoi = ex.Message;
                }
            }

            var choThem = _choKhoiDong - dongHo.Elapsed;
            if (choThem <= TimeSpan.Zero) break;
            await Task.Delay(choThem < _gianCachThuLai ? choThem : _gianCachThuLai, ct);
        }

        throw BaoLoiHeThong(
            ThongBaoSuCo,
            $"Dịch vụ không sẵn sàng sau {_choKhoiDong.TotalSeconds:0.#}s ({soLan} lần gọi /health), lỗi cuối: {loiCuoi}",
            null);
    }

    private ExternalServiceException BaoLoiHeThong(string thongBaoChoChu, string chiTietKyThuat, Exception? inner)
    {
        _logger.LogError(inner, "Ghép ảnh 360 thất bại do phía hệ thống: {Detail}", chiTietKyThuat);
        return new ExternalServiceException(TenDichVu, thongBaoChoChu, inner);
    }

    /// <summary>FastAPI HTTPException tra {"detail": "..."}; tra null neu khong phai chuoi.</summary>
    private static string? DocDetailChuoi(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            return doc.RootElement.ValueKind == JsonValueKind.Object
                   && doc.RootElement.TryGetProperty("detail", out var d)
                   && d.ValueKind == JsonValueKind.String
                   && !string.IsNullOrWhiteSpace(d.GetString())
                ? d.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string CatNgan(string text)
        => string.IsNullOrWhiteSpace(text) ? "(phản hồi rỗng)" : text.Length <= 500 ? text : text[..500] + "…";
}
