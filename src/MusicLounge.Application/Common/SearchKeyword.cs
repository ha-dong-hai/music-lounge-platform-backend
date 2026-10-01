namespace MusicLounge.Application.Common;

/// <summary>
/// MLACP-502. Một quy tắc chuẩn hoá từ khoá cho các danh sách có ô tìm kiếm (khiếu nại, hồ sơ phòng trà, buổi diễn,
/// phòng trà): bỏ khoảng trắng hai đầu, rỗng thì coi như không lọc, rồi hạ chữ thường để so với <c>cột.ToLower()</c>.
///
/// <para><b>Vì sao hạ chữ thường cả hai phía</b> thay vì dựa vào collation: SQL Server của dự án so không phân biệt hoa
/// thường nhưng SQLite dùng trong test thì có — để nguyên <c>Contains</c> thì test và production cho hai kết quả khác
/// nhau. <b>Trần đã biết:</b> hàm <c>lower()</c> của SQLite chỉ hạ chữ ASCII, nên trên SQLite "ĐÊM" không khớp "đêm";
/// trên SQL Server thì khớp. Test vì vậy kiểm hoa/thường bằng chữ không dấu.</para>
/// </summary>
public static class SearchKeyword
{
    public const int MaxLength = 200;

    public static string? Normalize(string? keyword)
    {
        var trimmed = keyword?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed.ToLowerInvariant();
    }
}
