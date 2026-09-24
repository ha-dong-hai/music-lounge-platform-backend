using System.Collections.Frozen;

namespace MusicLounge.Api.Localization;

/// <summary>
/// MLACP-487. Từ điển thông điệp tiếng Việt → tiếng Anh, tra ở cửa ra duy nhất
/// (<c>GlobalExceptionHandler</c>).
///
/// <para><b>VÌ SAO TRA THEO CHUỖI VIỆT CHỨ KHÔNG PHẢI THEO MÃ KHOÁ.</b> Cách "đúng sách" là đặt mã
/// cho từng thông điệp rồi sửa cả 438 chỗ gọi. Đo thật trước khi chọn: 438 chuỗi khác nhau nằm rải ở
/// <c>DomainException</c> (116), <c>ForbiddenException</c> (100), <c>ConflictException</c> (37),
/// <c>WithMessage</c> (251) — nhưng <b>chỉ 19 chuỗi có nội suy biến</b>, và <b>tất cả đều chảy qua
/// một chỗ</b>. Tra theo chuỗi phủ được phần áp đảo mà KHÔNG đụng vào 438 chỗ gọi, tức không có cơ
/// hội làm hỏng logic nghiệp vụ nào. Đổi lấy: khoá là chính câu tiếng Việt, nên sửa câu đó ở mã
/// nguồn mà quên sửa ở đây thì bản dịch lặng lẽ rơi mất.</para>
///
/// <para><b>Cái giá đó được chặn bằng test</b>, không bằng lời hứa: <c>ThongDiepSongNguTests</c>
/// quét mã nguồn và bắt lỗi mọi khoá không còn tồn tại. Đó là lý do <see cref="MoiKhoa"/> tồn tại.</para>
///
/// <para><b>LÙI VỀ TIẾNG VIỆT, KHÔNG BAO GIỜ TRẢ RỖNG.</b> Chưa dịch thì trả nguyên câu tiếng Việt.
/// Một thông điệp tiếng Việt giữa bản tiếng Anh thì hơi lệch, nhưng một ô trống hoặc một mã lỗi trần
/// thì người dùng không làm gì được với nó.</para>
///
/// <para><b>Biên dịch thẳng vào mã, không để file rời.</b> Ngày 23/09/2026 hệ thống mất toàn bộ
/// <c>/home</c> khi dời vùng Azure, kéo theo khoá Firebase, và mọi handler cần kho file trả 500. Một
/// từ điển đọc từ file lúc chạy sẽ thêm đúng kiểu phụ thuộc đó vào đường sinh thông điệp lỗi — tức
/// là thứ phải sống sót được cả khi những thứ khác đã hỏng.</para>
/// </summary>
internal static class ThongDiepSongNgu
{
    /// <summary>Trả bản tiếng Anh nếu có, không thì trả nguyên chuỗi vào.</summary>
    public static string Dich(string? viet)
        => string.IsNullOrEmpty(viet) ? viet ?? string.Empty
           : VietSangAnh.GetValueOrDefault(viet, viet);

    /// <summary>Cho test quét: mọi khoá ở đây phải còn tồn tại trong mã nguồn.</summary>
    public static IReadOnlyCollection<string> MoiKhoa => VietSangAnh.Keys;

    private static readonly FrozenDictionary<string, string> VietSangAnh =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            // ── Phân quyền ────────────────────────────────────────────────────────────────────
            ["Bạn không có quyền sửa venue này."] = "You do not have permission to edit this venue.",
            ["Bạn không có quyền sửa event này."] = "You do not have permission to edit this concert.",
            ["Bạn không có quyền quản lý menu cho venue này."] = "You do not have permission to manage the menu for this venue.",
            ["Bạn không có quyền quản lý khu vực chỗ ngồi cho venue này."] = "You do not have permission to manage seating zones for this venue.",
            ["Bạn không có quyền nộp duyệt event này."] = "You do not have permission to submit this concert for approval.",

            // ── Tham số không hợp lệ ──────────────────────────────────────────────────────────
            ["LoungeId không hợp lệ."] = "LoungeId is not valid.",
            ["ShowId không hợp lệ."] = "ShowId is not valid.",
            ["LivestreamId không hợp lệ."] = "LivestreamId is not valid.",
            ["Query params không được null."] = "Query parameters must not be null.",
            ["'From' phải trước hoặc bằng 'To'."] = "'From' must be earlier than or equal to 'To'.",

            // ── Tài khoản ─────────────────────────────────────────────────────────────────────
            ["Email không được để trống."] = "Email must not be empty.",
            ["Email không hợp lệ."] = "Email is not valid.",
            ["Mật khẩu không được để trống."] = "Password must not be empty.",
            ["Họ tên không được để trống."] = "Full name must not be empty.",

            // ── Câu chung của cửa ra ──────────────────────────────────────────────────────────
            ["Dữ liệu gửi lên không hợp lệ."] = "The submitted data is not valid.",
            ["Đã có lỗi hệ thống. Vui lòng thử lại sau."] = "A system error occurred. Please try again later.",
            ["Dữ liệu đã tồn tại hoặc xung đột, vui lòng thử lại."] = "This data already exists or conflicts with existing data; please try again.",

            // ── Duyệt hồ sơ ───────────────────────────────────────────────────────────────────
            ["Ghi chú duyệt không được vượt quá 1000 ký tự."] = "The review note must not exceed 1000 characters.",
        }
        .ToFrozenDictionary(StringComparer.Ordinal);
}
