namespace MusicLounge.Domain.ValueObjects;

// Được EF Core map thành cột phẳng trong bảng Lounges (OwnsOne - không tạo table riêng)
public sealed class VenueAddress
{
    public string Street { get; set; } = string.Empty;
    public string Ward { get; set; } = string.Empty;
    public string District { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    public string FullAddress =>
        string.Join(", ", new[] { Street, Ward, District, City }
            .Where(s => !string.IsNullOrWhiteSpace(s)));
}
