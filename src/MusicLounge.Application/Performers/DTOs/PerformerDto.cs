namespace MusicLounge.Application.Performers.DTOs;

public sealed record PerformerDto(
    int Id,
    string Name,
    string? AvatarUrl,
    string? Bio,
    string Type,
    int? CreatedByUserId,
    IReadOnlyList<int> GenreIds,
    IReadOnlyList<string> GenreNames,
    IReadOnlyList<PerformerSocialLinkDto> SocialLinks,
    // MLACP-467-class gap: UpdatePerformerCommand ghi ContactEmail nhưng DTO đọc trước đây không trả —
    // đây là nơi gửi liên kết PerformerConfirmation (MLACP-364, nghệ sĩ tự xác nhận đã nhận donate).
    // Sửa hồ sơ mà form không đọc lại được field này là xoá mất địa chỉ nhận xác nhận, hỏng chặng 2 của donate.
    string? ContactEmail = null);
