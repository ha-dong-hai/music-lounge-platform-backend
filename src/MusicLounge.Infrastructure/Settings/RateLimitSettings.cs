using Microsoft.Extensions.Configuration;

namespace MusicLounge.Infrastructure.Settings;

// Ngưỡng giới hạn tần suất là ngưỡng BẢO MẬT nên nằm ở appsettings, không ở system_config — cùng ranh giới với
// AuthLockoutSettings (xem chú thích ở đó). Trước đây hai con số 100 và 10 viết cứng trong Program.cs.
//
// VÌ SAO PHẢI CẤU HÌNH ĐƯỢC: bộ giới hạn chia theo địa chỉ IP. Khi trình diễn ở trường, cả hội trường đi ra Internet
// qua MỘT địa chỉ IP, nên 10 lần đăng nhập mỗi phút là ngưỡng của cả phòng chứ không phải của một người; người xem sẽ
// gặp 429 mà không hiểu vì sao. Môi trường trình diễn cần nâng được ngưỡng mà không sửa mã.
//
// MẶC ĐỊNH GIỮ NGUYÊN (100 và 10): không khai gì trong appsettings thì hành vi y như trước.
// TRẦN GIỚI HẠN + ĐƯỜNG NÂNG CẤP: vẫn chia theo IP. Cách đúng hơn cho lưu lượng thật sau NAT là chia theo tài khoản
// khi đã đăng nhập (đưa UseRateLimiter xuống sau UseAuthentication) — chưa làm vì đổi thứ tự middleware cần rà lại
// toàn bộ đường xác thực; làm khi có lưu lượng thật từ mạng dùng chung.
public sealed class RateLimitSettings
{
    public const string SectionName = "RateLimiting";
    public const int DefaultGlobalPermitPerMinute = 100;
    public const int DefaultAuthPermitPerMinute = 10;

    /// <summary>Số yêu cầu mỗi phút cho mỗi IP, áp dụng toàn API.</summary>
    public int GlobalPermitPerMinute { get; init; } = DefaultGlobalPermitPerMinute;

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
        return settings;
    }
}
