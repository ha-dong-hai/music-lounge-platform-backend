using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;
using MusicLounge.Application.Common.Models;
using MusicLounge.Infrastructure.Settings;

namespace MusicLounge.Api.Filters;

/// <summary>
/// MLACP-458. Xác thực máy trạm sinh poster bằng một khoá riêng trong header <c>X-Poster-Worker-Key</c>.
///
/// Vì sao không dùng tài khoản người dùng như mọi endpoint khác: máy trạm là một tiến trình chạy trên máy cá nhân của
/// người vận hành, không phải một con người có vai trò trong hệ thống. Cấp cho nó một tài khoản Admin để gọi ba endpoint
/// này đồng nghĩa đặt một tài khoản Admin sống mãi trong file cấu hình của một máy tính cá nhân — quyền lớn hơn rất nhiều
/// so với việc nó cần làm (nhận lời nhắc, nộp ảnh về).
///
/// Ba endpoint được bảo vệ bởi lớp này cố ý KHÔNG trả về dữ liệu cá nhân nào: chỉ có lời nhắc, vốn là thông tin sẽ in lên
/// chính tấm poster công khai.
/// </summary>
public sealed class PosterWorkerKeyAttribute : Attribute, IAsyncAuthorizationFilter
{
    public const string HeaderName = "X-Poster-Worker-Key";

    public Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var settings = context.HttpContext.RequestServices
            .GetRequiredService<IOptions<PosterWorkerSettings>>().Value;

        // Chưa cấu hình khoá thì KHÔNG có cửa nào mở: bỏ qua bước kiểm khi thiếu cấu hình sẽ biến một lần quên đặt biến
        // môi trường thành ba endpoint công khai.
        if (string.IsNullOrWhiteSpace(settings.ApiKey) || !KhopKhoa(context, settings.ApiKey))
        {
            context.Result = new ObjectResult(ApiResponse<object>.Fail("Khoá máy trạm không hợp lệ."))
            {
                StatusCode = StatusCodes.Status401Unauthorized
            };
        }

        return Task.CompletedTask;
    }

    /// <summary>So sánh theo thời gian cố định — so bằng <c>==</c> sẽ dừng ở ký tự khác đầu tiên và để lộ dần từng ký tự.</summary>
    private static bool KhopKhoa(AuthorizationFilterContext context, string expected)
    {
        if (!context.HttpContext.Request.Headers.TryGetValue(HeaderName, out var duocGui)) return false;

        var a = Encoding.UTF8.GetBytes(duocGui.ToString());
        var b = Encoding.UTF8.GetBytes(expected);
        return a.Length == b.Length && CryptographicOperations.FixedTimeEquals(a, b);
    }
}
