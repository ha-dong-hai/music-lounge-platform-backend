using MusicLounge.Application.Common.Models;

namespace MusicLounge.Application.LoungeShows.DTOs;

public sealed record ShowRatingItemDto(
    int Id,
    int? UserId,
    string? UserName,
    int Score,
    string? Comment,
    DateTimeOffset CreatedAt);

public sealed record ShowRatingsDto(
    decimal? AverageScore,
    int TotalCount,
    IReadOnlyDictionary<int, int> ScoreDistribution,
    PaginatedResult<ShowRatingItemDto> Items);
