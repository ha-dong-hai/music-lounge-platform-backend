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
    LivestreamPlaybackMode PlaybackMode,
    // Disclosed BEFORE the buyer pays. These columns existed and were enforced by CancelTicket,
    // but appeared in no DTO at all — so an audience member decided whether to buy without being
    // able to see whether the ticket was refundable, on what terms, or by when. Both NĐ 85/2021
    // (sàn phải công khai chính sách bảo vệ người mua) and every comparable platform treat
    // publishing this as a precondition of selling, not a nice-to-have.
    TicketRefundPolicyDto RefundPolicy,
    DateTimeOffset? TicketSaleClosesAt);

/// <param name="Summary">Ready-to-display Vietnamese sentence — built server-side so every client
/// states the same terms, and so the wording cannot drift from what CancelTicket actually enforces.</param>
public sealed record TicketRefundPolicyDto(
    bool CancellationAllowed,
    decimal RefundPercentage,
    DateTimeOffset? CancelBefore,
    int? DeadlineHoursBeforeStart,
    bool AlwaysFullRefundIfVenueCancels,
    string Summary);

// MLACP-60: "danh sach danh gia noi bat" — top danh gia co diem cao nhat va co binh luan (rating
// khong kem binh luan khong dang de hien thi thanh mot "review"). Loai danh gia da bi go (IsRemoved).
public sealed record FeaturedRatingDto(
    int Score,
    string Comment,
    string ReviewerName,
    string? ReviewerAvatarUrl,
    DateTimeOffset CreatedAt);
