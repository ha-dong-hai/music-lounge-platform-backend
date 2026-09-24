using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.ValueObjects;

namespace MusicLounge.Api.Localization;

/// <summary>
/// MLACP-489. Hiện thực <see cref="IRequestLanguage"/> bằng chính bộ đọc <c>Accept-Language</c> của MLACP-487
/// (<see cref="NgonNguYeuCau"/>) — cùng một quy tắc thương lượng (trọng số q, thẻ có vùng, ranh giới thẻ) cho thông
/// điệp lỗi lẫn nội dung thông báo, để hai thứ không bao giờ chọn hai ngôn ngữ khác nhau trên cùng một request.
///
/// Không có request (job Hangfire chạy nền) thì trả tiếng Việt.
/// </summary>
internal sealed class HttpRequestLanguage : IRequestLanguage
{
    private readonly IHttpContextAccessor _http;

    public HttpRequestLanguage(IHttpContextAccessor http) => _http = http;

    public string Current
        => _http.HttpContext is { } ctx && NgonNguYeuCau.MuonTiengAnh(ctx.Request.Headers.AcceptLanguage)
            ? NgonNgu.Anh
            : NgonNgu.Viet;
}
