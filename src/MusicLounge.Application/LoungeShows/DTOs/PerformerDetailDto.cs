using MusicLounge.Application.Common.Models;

namespace MusicLounge.Application.LoungeShows.DTOs;

public sealed record PerformerDetailDto(
    Guid Id,
    string Name,
    string? AvatarUrl,
    string? Bio,
    IReadOnlyList<GenreDto> Genres,
    PaginatedResult<LoungeShowListItemDto> Shows);
