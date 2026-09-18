using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;

namespace MusicLounge.Api.Middleware;

/// <summary>
/// MLACP-448. 401 và 403 do middleware phân quyền sinh ra trước đây trả **body rỗng** (production:
/// <c>Content-Length: 0</c>), trong khi mọi lỗi khác đều có <c>{success, message, errors}</c> từ
/// <see cref="GlobalExceptionHandler"/> — frontend phải xử lý hai kiểu phản hồi lỗi khác nhau.
///
/// <para>Dùng <see cref="IAuthorizationMiddlewareResultHandler"/>, điểm mở rộng chính thức của ASP.NET Core cho đúng việc
/// này ("Customize the behavior of AuthorizationMiddleware"). Bộ xử lý mặc định chạy TRƯỚC: nó gọi Challenge/Forbid của
/// scheme JwtBearer, và chính scheme đặt mã 401/403 cùng header <c>WWW-Authenticate</c> — header mà RFC 6750 §3 bắt buộc
/// có khi trả 401. Scheme không ghi body và chưa bắt đầu phản hồi, nên sau đó mới ghi body JSON. Không đổi mã trạng thái
/// nào, không bỏ header nào.</para>
/// </summary>
internal sealed class JsonAuthorizationResultHandler : IAuthorizationMiddlewareResultHandler
{
    internal const string ChuaDangNhap = "Bạn cần đăng nhập để thực hiện thao tác này.";
    internal const string PhienHetHan = "Phiên đăng nhập không hợp lệ hoặc đã hết hạn. Vui lòng đăng nhập lại.";
    internal const string KhongCoQuyen = "Bạn không có quyền thực hiện thao tác này.";

    private readonly AuthorizationMiddlewareResultHandler _macDinh = new();
    private readonly ILogger<JsonAuthorizationResultHandler> _logger;

    public JsonAuthorizationResultHandler(ILogger<JsonAuthorizationResultHandler> logger) => _logger = logger;

    public async Task HandleAsync(
        RequestDelegate next, HttpContext context, AuthorizationPolicy policy, PolicyAuthorizationResult authorizeResult)
    {
        await _macDinh.HandleAsync(next, context, policy, authorizeResult);

        if (!(authorizeResult.Challenged || authorizeResult.Forbidden) || context.Response.HasStarted)
            return;

        string message;
        if (authorizeResult.Forbidden)
        {
            message = KhongCoQuyen;
            // Cùng tên sự kiện với ForbiddenException trong GlobalExceptionHandler: một loạt 403 từ cùng một tài khoản
            // là dấu hiệu dò quyền, và phải lọc được trong log như nhau dù 403 đến từ middleware hay từ handler.
            var userId = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "anonymous";
            _logger.LogWarning(
                "Authorization denied: UserId={UserId} Path={Path} Method={Method} Message={Message} at {At}",
                userId, context.Request.Path, context.Request.Method, message, DateTimeOffset.UtcNow);
        }
        else
        {
            // Có gửi token mà vẫn bị 401 nghĩa là token hỏng hoặc hết hạn — frontend dựa vào đây để làm mới phiên
            // thay vì đẩy người dùng về trang đăng nhập như với người chưa đăng nhập.
            message = context.Request.Headers.Authorization.Count > 0 ? PhienHetHan : ChuaDangNhap;
        }

        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(
            new { success = false, message, errors = (object?)null }, context.RequestAborted);
    }
}
