namespace MusicLounge.Application.Performers.DTOs;

public sealed record PerformerSocialLinkDto(
    int Id,
    string Platform,
    string Url,
    string? DisplayName);
