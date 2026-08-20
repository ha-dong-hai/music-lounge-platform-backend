using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.Livestreams.DTOs;

public sealed record LivestreamDetailDto(
    int Id,
    int LoungeShowId,
    string ShowName,
    LivestreamStatus Status,
    string? HlsUrl,
    int ViewerCount,
    DateTimeOffset? StartedAt,
    DateTimeOffset? EndedAt,
    string? TerminatedReason,
    bool UserHasAccess);
