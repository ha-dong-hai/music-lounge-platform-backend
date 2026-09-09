namespace MusicLounge.Application.Lounges.DTOs;

/// <param name="HasBusinessLicense">
/// Giấy phép kinh doanh là thứ chính để duyệt một phòng trà, và nó có thể trống — trường
/// BusinessLicenseUrl vốn nullable, không có ràng buộc nào bắt phải nộp trước khi tạo. Đưa cờ này
/// ra ngay trong danh sách để Admin thấy hồ sơ nào chưa đủ căn cứ mà xét, thay vì mở từng cái ra
/// mới biết.
/// </param>
/// <param name="ReviewNote">Ghi chú của lần xét trước — có giá trị khi lọc danh sách đã bị từ chối.</param>
public sealed record VenueReviewItemDto(
    int LoungeId,
    string Name,
    string Status,
    int OwnerId,
    string OwnerName,
    string OwnerEmail,
    string? OwnerPhone,
    string FullAddress,
    string? PrimaryImageUrl,
    bool HasBusinessLicense,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StatusReviewedAt,
    string? ReviewNote);
