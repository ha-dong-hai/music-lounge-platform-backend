namespace MusicLounge.Application.Staffing.DTOs;

public sealed record LoungeStaffDto(
    Guid Id,
    Guid UserId,
    string FullName,
    string Email,
    bool IsActive,
    DateTimeOffset AssignedAt,
    DateTimeOffset? DeactivatedAt,
    Guid? DeactivatedBy);
