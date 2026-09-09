namespace MusicLounge.Application.Mutes.DTOs;

/// <summary>
/// Một phòng trà người dùng đã tắt tiếng. Cố ý gọn hơn <c>FollowedLoungeDto</c>: đây là màn hình
/// quản lý để xem lại và bỏ tắt tiếng, không phải màn hình khám phá cần ảnh bìa.
/// </summary>
public sealed record MutedLoungeDto(
    int Id,
    string Name,
    string District,
    string City,
    DateTimeOffset MutedAt);
