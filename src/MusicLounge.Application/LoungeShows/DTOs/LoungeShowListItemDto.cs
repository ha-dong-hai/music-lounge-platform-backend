using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.LoungeShows.DTOs;

public sealed record LoungeShowListItemDto(
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
    int? OfflineQuota,
    int? OnlineQuota,
    bool? IsWishlisted);
