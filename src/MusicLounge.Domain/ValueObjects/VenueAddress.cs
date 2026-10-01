namespace MusicLounge.Domain.ValueObjects;

// Được EF Core map thành cột phẳng trong bảng Lounges (OwnsOne - không tạo table riêng)
public sealed class VenueAddress
{
    public string Street { get; set; } = string.Empty;
    public string Ward { get; set; } = string.Empty;
    public string District { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;

    // MLACP-521: mã đơn vị hành chính chính thức (QĐ 19/2025/QĐ-TTg) — tỉnh 2 chữ số, phường/xã 5 chữ số. Có mã thì
    // City/Ward là tên lấy từ danh mục, District để trống (không còn cấp huyện từ 01/7/2025). null = địa chỉ cũ gõ tay,
    // chưa chọn theo danh mục mới (phường cũ đã bị tách/gộp nên không tự đoán mã phường).
    public string? ProvinceCode { get; set; }
    public string? WardCode { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    public string FullAddress =>
        string.Join(", ", new[] { Street, Ward, District, City }
            .Where(s => !string.IsNullOrWhiteSpace(s)));
}
