namespace MusicLounge.Application.Users.DTOs;

public sealed record UserAdminDto(
    int Id,
    string Email,
    string FullName,
    string? Phone,
    string? AvatarUrl,
    string Role,
    bool IsActive,
    bool IsEmailVerified,
    /// <summary>
    /// <c>DateTimeOffset</c>, không phải <c>DateTime</c> — xem lý do ở
    /// <c>PayoutAccountReviewItemDto.CreatedAt</c>: mốc không mang múi giờ thì trình duyệt hiểu là giờ
    /// địa phương và hiện lệch 7 tiếng ở Việt Nam. (MLACP-475)
    /// </summary>
    DateTimeOffset CreatedAt);
