using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.LoungeShows.DTOs;

public sealed record LoungeShowDetailDto(
    int Id,
    string Name,
    string Description,
    string? CoverImageUrl,
    DateTimeOffset ScheduledStart,
    DateTimeOffset? ScheduledEnd,
    LoungeShowFormat Format,
    LoungeShowStatus Status,
    bool IsOngoing,
    int? LivestreamId,
    LoungeSummaryDto Lounge,
    IReadOnlyList<PerformerSummaryDto> Performers,
    IReadOnlyList<TicketTierSummaryDto> TicketTiers,
    IReadOnlyList<GenreDto> Genres,
    RatingSummaryDto Ratings,
    bool? IsWishlisted,
    bool? UserHasTicket,
    bool? UserHasRated,
    string? LegalApprovalReference,
    bool LegalApprovalConfirmed,
    string? VcpmcRoyaltyReference,
    LivestreamPlaybackMode PlaybackMode);
