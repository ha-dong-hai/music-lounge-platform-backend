using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.LoungeShows.DTOs;

public sealed record PerformerSummaryDto(
    int Id,
    string Name,
    string? AvatarUrl,
    string? Bio,
    IReadOnlyList<GenreDto> Genres,
    int PerformanceId,
    bool AcceptsDonation,
    PerformerRole Role,
    TimeOnly? SetTime);
