namespace MusicLounge.Application.Auth.DTOs;

public sealed record RegisterResultDto(
    string Email,
    string FullName,
    DateTimeOffset VerificationCodeExpiresAt);
