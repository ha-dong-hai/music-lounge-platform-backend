namespace MusicLounge.Api.Localization;

/// <summary>
/// MLACP-487. Đọc <c>Accept-Language</c> để quyết định trả thông điệp tiếng Việt hay tiếng Anh.
///
/// <para>Yêu cầu phi chức năng đã đăng ký trong văn bản đề tài (05/04/2026, mục Non-functional
/// requirement): <i>"Bilingual interface: Vietnamese &amp; English."</i> Backend trước đây sinh mọi
/// thông điệp bằng tiếng Việt cứng — không có <c>.resx</c>, không <c>IStringLocalizer</c>, không
/// <c>AddLocalization</c> — nên phía client không có cách nào xin bản tiếng Anh.</para>
///
/// <para>TIẾNG VIỆT LÀ MẶC ĐỊNH, không phải tiếng Anh. Người dùng thật của hệ thống là khách và chủ
/// phòng trà ở Việt Nam; client không khai gì thì phải nhận đúng thứ họ vẫn nhận từ trước, chứ một
/// bản cập nhật không được phép đổi ngôn ngữ của những người đang dùng.</para>
///
/// <para>Hàm thuần, không phụ thuộc <c>HttpContext</c>, để test được mà không cần dựng server.</para>
/// </summary>
internal static class NgonNguYeuCau
{
    /// <summary>
    /// Trả <c>true</c> khi client ưu tiên tiếng Anh hơn tiếng Việt.
    ///
    /// <para>Xử lý đúng ba thứ mà một bộ đọc ngây thơ hay sai:</para>
    /// <list type="number">
    /// <item>THỨ TỰ ƯU TIÊN theo trọng số <c>q</c>, không phải theo thứ tự xuất hiện.
    /// <c>"vi;q=0.9, en;q=0.8"</c> là ưu tiên tiếng Việt dù <c>en</c> đứng sau.</item>
    /// <item>Thẻ có VÙNG: <c>en-US</c>, <c>en-GB</c> đều là tiếng Anh; <c>vi-VN</c> là tiếng Việt.
    /// So sánh cả chuỗi thì <c>en-US</c> sẽ trượt.</item>
    /// <item>Tiền tố KHÔNG phải ngôn ngữ đó: <c>"enm"</c> (tiếng Anh trung đại) bắt đầu bằng "en"
    /// nhưng không phải <c>en</c>. Phải so theo RANH GIỚI THẺ, không so tiền tố trần — cùng lớp lỗi
    /// mà bài học L-12 đã ghi cho đường dẫn.</item>
    /// </list>
    ///
    /// <para>Không nhận ra gì thì trả <c>false</c> (tiếng Việt). Khoảng lặng không bao giờ được hiểu
    /// thành "đổi ngôn ngữ".</para>
    /// </summary>
    public static bool MuonTiengAnh(string? acceptLanguage)
    {
        if (string.IsNullOrWhiteSpace(acceptLanguage)) return false;

        double diemEn = -1, diemVi = -1;

        foreach (var phan in acceptLanguage.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var manh = phan.Split(';', StringSplitOptions.TrimEntries);
            var the = manh[0].Trim();
            if (the.Length == 0) continue;

            var diem = 1.0;
            foreach (var tham in manh.Skip(1))
            {
                if (!tham.StartsWith("q=", StringComparison.OrdinalIgnoreCase)) continue;
                // q sai định dạng thì coi như client không nêu trọng số, giữ mặc định 1.0 —
                // an toàn hơn là vứt cả thẻ đi.
                if (double.TryParse(tham[2..], System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out var q))
                    diem = q;
            }

            if (LaNgonNgu(the, "en")) diemEn = Math.Max(diemEn, diem);
            else if (LaNgonNgu(the, "vi")) diemVi = Math.Max(diemVi, diem);
        }

        // Hoà thì nghiêng về tiếng Việt: "en;q=1, vi;q=1" nghĩa là client không thật sự chọn bên nào.
        return diemEn > 0 && diemEn > diemVi;
    }

    /// <summary>
    /// So theo RANH GIỚI THẺ chứ không so tiền tố: <c>en</c> khớp <c>en</c> và <c>en-US</c>, nhưng
    /// KHÔNG khớp <c>enm</c>.
    /// </summary>
    private static bool LaNgonNgu(string the, string ma)
        => the.Equals(ma, StringComparison.OrdinalIgnoreCase)
           || (the.Length > ma.Length
               && the.StartsWith(ma, StringComparison.OrdinalIgnoreCase)
               && the[ma.Length] == '-');
}
