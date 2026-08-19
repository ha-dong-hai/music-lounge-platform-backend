namespace MusicLounge.Application.Auth.DTOs;

public sealed record ResendVerificationCodeResultDto(DateTimeOffset VerificationCodeExpiresAt);
