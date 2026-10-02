namespace MusicLounge.Application.Auth.DTOs;

public sealed record AuthResultDto(
    string Token,
    DateTimeOffset ExpiresAt,
    Guid UserId,
    string Email,
    string FullName,
    string Role,
    Guid? LoungeId = null,
    string? RefreshToken = null,
    DateTimeOffset? RefreshTokenExpiresAt = null);
