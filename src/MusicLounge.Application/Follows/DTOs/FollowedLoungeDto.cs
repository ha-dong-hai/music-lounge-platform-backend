namespace MusicLounge.Application.Follows.DTOs;

public sealed record FollowedLoungeDto(
    int Id,
    string Name,
    string? PrimaryImageUrl,
    string District,
    string City,
    DateTimeOffset FollowedAt);
