using Microsoft.Extensions.Configuration;

namespace MusicLounge.Infrastructure.Settings;

// Ngưỡng giới hạn tần suất là ngưỡng BẢO MẬT nên nằm ở appsettings, không ở system_config — cùng ranh giới với
// AuthLockoutSettings (xem chú thích ở đó). Trước đây hai con số 100 và 10 viết cứng trong Program.cs.
//
// VÌ SAO PHẢI CẤU HÌNH ĐƯỢC: bộ giới hạn chia theo địa chỉ IP. Khi trình diễn ở trường, cả hội trường đi ra Internet
// qua MỘT địa chỉ IP, nên 10 lần đăng nhập mỗi phút là ngưỡng của cả phòng chứ không phải của một người; người xem sẽ
// gặp 429 mà không hiểu vì sao. Môi trường trình diễn cần nâng được ngưỡng mà không sửa mã.
//
// MẶC ĐỊNH: khách 100/IP, đăng nhập/đăng ký 10/IP như trước; người đã đăng nhập 300/tài khoản (MLACP-686).
// MLACP-670 (05/10/2026): ngưỡng chung nay chia theo TÀI KHOẢN khi đã đăng nhập, theo IP khi chưa (RateLimitPartitionKey);
// UseRateLimiter đã xuống sau UseAuthentication. Ngưỡng đăng nhập/đăng ký vẫn theo IP. Nên ngưỡng auth vẫn là ngưỡng
// của cả phòng khi trình diễn — nâng AuthPermitPerMinute cho môi trường đó nếu cần.
public sealed class RateLimitSettings
{
    public const string SectionName = "RateLimiting";
    public const int DefaultGlobalPermitPerMinute = 100;
    public const int DefaultAuthPermitPerMinute = 10;
    public const int DefaultUserPermitPerMinute = 300;

    /// <summary>Số yêu cầu mỗi phút, áp dụng toàn API cho mỗi IP CHƯA đăng nhập (khách).</summary>
    public int GlobalPermitPerMinute { get; init; } = DefaultGlobalPermitPerMinute;

    /// <summary>
    /// MLACP-686. Số yêu cầu mỗi phút cho mỗi TÀI KHOẢN đã đăng nhập. Tách khỏi ngưỡng khách vì một người dùng thật đã chạm
    /// 100/phút: đo 05/10/2026 mở 21 trang/phút đã nhận 429 (mỗi trang 4–13 yêu cầu + kênh thời gian thực), và từ MLACP-685
    /// mỗi nhóm tab đếm số của từng tab (thêm 2–6 yêu cầu nhỏ mỗi trang). 300 = 5 yêu cầu/giây/tài khoản: đủ cho người lướt
    /// nhanh, vẫn chặn được một tài khoản bị dùng để quét. Khách vẫn 100/IP — khách không có danh tính để khoá, và đó là
    /// đường của kẻ quét không đăng nhập.
    /// </summary>
    public int UserPermitPerMinute { get; init; } = DefaultUserPermitPerMinute;

    /// <summary>Số yêu cầu mỗi phút cho mỗi IP ở nhóm đăng nhập/đăng ký (cộng dồn với ngưỡng chung).</summary>
    public int AuthPermitPerMinute { get; init; } = DefaultAuthPermitPerMinute;

    /// <summary>
    /// Đọc từ cấu hình. Giá trị từ 0 trở xuống là cấu hình SAI và làm dừng ngay lúc khởi động — không lặng lẽ thay bằng
    /// mặc định (người vận hành tưởng đã nâng ngưỡng) và không coi 0 là "tắt giới hạn" (tắt nhầm lớp chống dò mật khẩu).
    /// </summary>
    public static RateLimitSettings From(IConfiguration configuration)
    {
        var settings = configuration.GetSection(SectionName).Get<RateLimitSettings>() ?? new RateLimitSettings();
        if (settings.GlobalPermitPerMinute <= 0)
            throw new InvalidOperationException(
                $"{SectionName}:{nameof(GlobalPermitPerMinute)} phải lớn hơn 0 (đang là {settings.GlobalPermitPerMinute}).");
        if (settings.AuthPermitPerMinute <= 0)
            throw new InvalidOperationException(
                $"{SectionName}:{nameof(AuthPermitPerMinute)} phải lớn hơn 0 (đang là {settings.AuthPermitPerMinute}).");
        if (settings.UserPermitPerMinute <= 0)
            throw new InvalidOperationException(
                $"{SectionName}:{nameof(UserPermitPerMinute)} phải lớn hơn 0 (đang là {settings.UserPermitPerMinute}).");
        return settings;
    }
}
