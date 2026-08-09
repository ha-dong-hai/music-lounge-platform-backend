namespace MusicLounge.Application.Performers.DTOs;

public sealed record PerformerDto(
    int Id,
    string Name,
    string? AvatarUrl,
    string? Bio,
    string Type,
    int? CreatedByUserId,
    IReadOnlyList<int> GenreIds,
    IReadOnlyList<string> GenreNames,
    IReadOnlyList<PerformerSocialLinkDto> SocialLinks);
