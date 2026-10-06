using System.Security.Claims;
using MusicLounge.Infrastructure.Settings;

namespace MusicLounge.Api.RateLimiting;

/// <summary>
/// MLACP-670. Khoá chia hạn mức của bộ giới hạn chung: người đã đăng nhập tính theo TÀI KHOẢN, khách tính theo IP.
///
/// <para>Trước đây mọi yêu cầu tính theo IP. Ở mạng dùng chung (WiFi trường trong buổi bảo vệ, quán cà phê, NAT nhà mạng)
/// cả nhóm chung một hạn mức 100 yêu cầu/phút, nên người này dùng thì người kia nhận 429 — đo được ngày 05/10 khi web có
/// kênh thời gian thực: mỗi lần mở trang thêm 2 yêu cầu (negotiate + WebSocket), đủ đẩy một lượt xem liên tục quá ngưỡng.</para>
///
/// <para>Nhóm đăng nhập/đăng ký (policy "auth") VẪN theo IP: lúc đó chưa có tài khoản, và đó là lớp chống dò mật khẩu.
/// Khoá có tiền tố để mã người dùng và địa chỉ IP không bao giờ trùng một ngăn.</para>
/// </summary>
public static class RateLimitPartitionKey
{
    private const string TienToNguoiDung = "user:";

    public static string For(HttpContext ctx)
    {
        if (ctx.User.Identity?.IsAuthenticated == true
            && Guid.TryParse(ctx.User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            return $"{TienToNguoiDung}{userId}";
        return $"ip:{ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";
    }

    /// <summary>
    /// MLACP-686. Ngưỡng của một ngăn: tài khoản đã đăng nhập dùng <see cref="RateLimitSettings.UserPermitPerMinute"/>, khách
    /// (theo IP) dùng <see cref="RateLimitSettings.GlobalPermitPerMinute"/>. Quyết theo khoá (không đọc lại HttpContext) để
    /// ngăn và ngưỡng của ngăn không bao giờ lệch nhau: khoá do <see cref="For"/> tạo, và chỉ khoá đã qua xác thực mới mang
    /// tiền tố "user:".
    /// </summary>
    public static int PermitFor(string partitionKey, RateLimitSettings settings)
        => partitionKey.StartsWith(TienToNguoiDung, StringComparison.Ordinal)
            ? settings.UserPermitPerMinute
            : settings.GlobalPermitPerMinute;
}
