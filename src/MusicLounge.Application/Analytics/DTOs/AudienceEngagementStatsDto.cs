namespace MusicLounge.Application.Analytics.DTOs;

public sealed record AudienceEngagementStatsDto(
    DateTimeOffset PeriodFrom,
    DateTimeOffset PeriodTo,
    int NewFollowsInPeriod,
    int NewWishlistsInPeriod,
    int NewRatingsInPeriod,
    decimal ReturnRatePercent);
