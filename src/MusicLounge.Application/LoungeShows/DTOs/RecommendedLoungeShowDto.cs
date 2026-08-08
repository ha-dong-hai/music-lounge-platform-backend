using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.LoungeShows.DTOs;

public sealed record RecommendedLoungeShowDto(
    int Id,
    string Name,
    string? CoverImageUrl,
    string LoungeName,
    string LoungeDistrict,
    string LoungeCity,
    DateTimeOffset ScheduledStart,
    LoungeShowFormat Format,
    LoungeShowStatus Status,
    decimal? MinPrice,
    decimal? MaxPrice,
    IReadOnlyList<GenreDto> Genres,
    IReadOnlyList<string> PerformerNames,
    float RecommendationScore,
    string RecommendationReason);
