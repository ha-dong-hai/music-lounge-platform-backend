namespace MusicLounge.Application.Staffing.DTOs;

public sealed record LoungeStaffDto(
    int Id,
    int UserId,
    string FullName,
    string Email,
    bool IsActive,
    DateTimeOffset AssignedAt,
    DateTimeOffset? DeactivatedAt);
