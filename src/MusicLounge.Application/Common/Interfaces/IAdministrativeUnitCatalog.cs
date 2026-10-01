namespace MusicLounge.Application.Common.Interfaces;

/// <summary>
/// MLACP-521. Danh mục đơn vị hành chính HAI CẤP (tỉnh → phường/xã) từ 01/7/2025: 34 tỉnh, 3.321 xã theo
/// QĐ 19/2025/QĐ-TTg (Nghị quyết 202/2025/QH15 bỏ cấp huyện). Mã là mã chính thức của Cục Thống kê, nên lưu MÃ chứ
/// không lưu tên gõ tay — tên tự lấy từ danh mục, không còn "TP.HCM"/"Hồ Chí Minh"/"HCM" là ba thành phố khác nhau.
///
/// <para><b>Trần đã biết.</b> Danh mục nạp từ tệp nhúng khi khởi động (dữ liệu tham chiếu tĩnh, chỉ đổi khi có quyết
/// định mới) — đổi danh mục là thay tệp rồi deploy. Khi cần Admin tự sửa danh mục hoặc lưu lịch sử đổi tên/sáp nhập
/// thì chuyển sang bảng trong DB; hai cột mã trên địa chỉ phòng trà giữ nguyên.</para>
/// </summary>
public interface IAdministrativeUnitCatalog
{
    IReadOnlyList<AdministrativeProvince> Provinces { get; }

    AdministrativeProvince? FindProvince(string? code);

    AdministrativeWard? FindWard(string? code);

    /// <summary>Rỗng nếu mã tỉnh không có trong danh mục.</summary>
    IReadOnlyList<AdministrativeWard> WardsOf(string provinceCode);
}

public sealed record AdministrativeProvince(string Code, string Name, string DivisionType);

public sealed record AdministrativeWard(string Code, string Name, string DivisionType, string ProvinceCode);
