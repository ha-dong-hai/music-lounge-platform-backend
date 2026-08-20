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
    DateTime CreatedAt);
