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
    IReadOnlyList<MoodDto> Moods,
    IReadOnlyList<AtmosphereDto> Atmospheres,
    RatingSummaryDto Ratings,
    IReadOnlyList<FeaturedRatingDto> FeaturedRatings,
    bool? IsWishlisted,
    bool? UserHasTicket,
    bool? UserHasRated,
    bool LegalApprovalConfirmed,
    LivestreamPlaybackMode PlaybackMode);

// MLACP-60: "danh sach danh gia noi bat" — top danh gia co diem cao nhat va co binh luan (rating
// khong kem binh luan khong dang de hien thi thanh mot "review"). Loai danh gia da bi go (IsRemoved).
public sealed record FeaturedRatingDto(
    int Score,
    string Comment,
    string ReviewerName,
    string? ReviewerAvatarUrl,
    DateTimeOffset CreatedAt);
