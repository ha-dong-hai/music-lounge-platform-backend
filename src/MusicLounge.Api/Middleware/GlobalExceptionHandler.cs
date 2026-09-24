using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using MusicLounge.Api.Localization;
using MusicLounge.Application.Common.Exceptions;
using MusicLounge.Domain.Exceptions;

namespace MusicLounge.Api.Middleware;

internal sealed class GlobalExceptionHandler : IExceptionHandler
{
    /// <summary>
    /// MLACP-448: câu cho mọi lỗi 500 không lường trước. Trước đây là tiếng Anh ("An unexpected error occurred.") trong một
    /// app tiếng Việt. Cố ý không kèm chi tiết ngoại lệ — chi tiết chỉ nằm trong log, không lộ ra ngoài.
    /// </summary>
    internal const string ThongBaoLoiHeThong = "Đã có lỗi hệ thống. Vui lòng thử lại sau.";

    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) => _logger = logger;

    public async ValueTask<bool> TryHandleAsync(
        HttpContext ctx, Exception ex, CancellationToken ct)
    {
        var (status, message, errors) = ex switch
        {
            NotFoundException e      => (StatusCodes.Status404NotFound,           e.Message,  (object?)null),
            UnauthorizedException e  => (StatusCodes.Status401Unauthorized,        e.Message,  null),
            ForbiddenException e     => (StatusCodes.Status403Forbidden,           e.Message,  null),
            ConflictException e      => (StatusCodes.Status409Conflict,            e.Message,  null),
            DomainException e        => (StatusCodes.Status422UnprocessableEntity, e.Message,  null),
            ValidationException e    => (StatusCodes.Status400BadRequest,          ValidationException.ThongBaoChung, (object?)e.Errors),
            ExternalServiceException e => (StatusCodes.Status503ServiceUnavailable, e.Message, null),
            // Race condition hiem (2 request dong thoi cung vi pham 1 unique constraint, vd trung
            // email luc dang ky) — DB da chan dung du lieu, chi can tra ve 409 sach thay vi de lot
            // thanh 500 tho gay hieu lam la loi server.
            DbUpdateException           => (StatusCodes.Status409Conflict, "Dữ liệu đã tồn tại hoặc xung đột, vui lòng thử lại.", null),
            _                        => (StatusCodes.Status500InternalServerError, ThongBaoLoiHeThong, null)
        };

        // DbUpdateException tra 409 than thien cho nguoi dung, nhung van la 1 loi DB khong luong
        // truoc duoc (co the la unique-constraint vo hai nhu trung email, cung co the la FK
        // violation/deadlock/timeout that su) — log ERROR de operator van thay, du response
        // khong can bao dong voi nguoi dung.
        if (status >= 500 || ex is DbUpdateException)
        {
            _logger.LogError(ex, "Unhandled server error: {Message}", ex.Message);
        }
        else if (ex is ForbiddenException)
        {
            // Distinct event name/level from routine business exceptions on purpose — a spike of
            // these against different resource IDs from the same account is exactly the signature of
            // IDOR/cross-tenant probing, and it was previously indistinguishable in logs from a
            // routine "ticket not found" (both landed on the same generic "Business exception" line).
            // Nothing currently ships this anywhere alert-able (see role-devops-release-readiness's
            // finding — Console + local rolling file only), but a distinct, filterable event name is
            // the prerequisite for that to ever be wired up.
            var userId = ctx.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "anonymous";
            _logger.LogWarning(
                "Authorization denied: UserId={UserId} Path={Path} Method={Method} Message={Message} at {At}",
                userId, ctx.Request.Path, ctx.Request.Method, ex.Message, DateTimeOffset.UtcNow);
        }
        else
        {
            _logger.LogWarning("Business exception {ExceptionType}: {Message}", ex.GetType().Name, ex.Message);
        }

        ctx.Response.ContentType = "application/json";
        ctx.Response.StatusCode = status;

        // MLACP-487: yêu cầu phi chức năng đã đăng ký trong văn bản đề tài (05/04/2026) —
        // "Bilingual interface: Vietnamese & English". Dịch Ở ĐÂY chứ không ở 438 chỗ ném lỗi: mọi
        // thông điệp gửi ra người dùng đều đi qua đúng cửa này, nên một chỗ móc là đủ, và không có
        // dòng logic nghiệp vụ nào bị đụng tới.
        //
        // Ghi log Ở TRÊN vẫn giữ nguyên tiếng Việt: log là để người vận hành đối chiếu với mã nguồn,
        // mà mã nguồn viết tiếng Việt. Dịch log sẽ làm grep từ báo cáo lỗi về mã nguồn đứt đoạn.
        if (NgonNguYeuCau.MuonTiengAnh(ctx.Request.Headers.AcceptLanguage))
        {
            // NotFoundException dựng câu bằng khuôn ($"Không tìm thấy {nhãn} (mã {key})."), nên tra
            // từ điển theo chuỗi nguyên văn KHÔNG BAO GIỜ khớp — mỗi mã khoá cho ra một câu khác.
            // Dựng lại từ ResourceName + Key vốn đã phơi sẵn, thay vì bóc tách chuỗi đã ghép bằng
            // regex (vỡ ngay lần đầu ai sửa dấu câu). Một khuôn này phủ 270 chỗ gọi.
            message = ex is NotFoundException nf
                ? NotFoundException.CauTiengAnh(nf.ResourceName, nf.Key)
                : ThongDiepSongNgu.Dich(message);
            errors = DichLoiTungTruong(errors);
        }

        await ctx.Response.WriteAsJsonAsync(
            new { success = false, message, errors },
            ct);

        return true;
    }

    /// <summary>
    /// MLACP-487. Lỗi kiểm dữ liệu nằm ở <c>ValidationException.Errors</c> dạng
    /// <c>Dictionary&lt;tên trường, string[]&gt;</c> — tức 251 câu <c>WithMessage</c> KHÔNG đi qua
    /// trường <c>message</c> mà đi qua đây. Bỏ sót chỗ này thì bản tiếng Anh vẫn hiện nguyên tiếng
    /// Việt ở đúng nơi người dùng đọc nhiều nhất: dưới từng ô nhập.
    ///
    /// <para>TÊN TRƯỜNG giữ nguyên, không dịch: client dùng nó để gắn lỗi vào đúng ô. Dịch tên
    /// trường là làm hỏng chức năng để đổi lấy một thứ người dùng không nhìn thấy.</para>
    /// </summary>
    private static object? DichLoiTungTruong(object? errors)
        => errors is IReadOnlyDictionary<string, string[]> theoTruong
            ? theoTruong.ToDictionary(
                c => c.Key,
                c => Array.ConvertAll(c.Value, ThongDiepSongNgu.Dich))
            : errors;
}
