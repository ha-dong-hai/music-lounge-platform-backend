namespace MusicLounge.Application.Auth.DTOs;

public sealed record AuthResultDto(
    string Token,
    DateTimeOffset ExpiresAt,
    int UserId,
    string Email,
    string FullName,
    string Role,
    int? LoungeId = null);
