namespace MusicLounge.Application.Performers.DTOs;

public sealed record PerformerSocialLinkDto(
    Guid Id,
    string Platform,
    string Url,
    string? DisplayName);
