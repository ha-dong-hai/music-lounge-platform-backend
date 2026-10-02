namespace MusicLounge.Application.Follows.DTOs;

public sealed record FollowedLoungeDto(
    Guid Id,
    string Name,
    string? PrimaryImageUrl,
    string District,
    string City,
    DateTimeOffset FollowedAt);
