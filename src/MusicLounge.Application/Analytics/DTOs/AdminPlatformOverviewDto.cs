namespace MusicLounge.Application.Analytics.DTOs;

public sealed record AdminPlatformOverviewDto(
    DateTimeOffset PeriodFrom,
    DateTimeOffset PeriodTo,
    int ActiveVenuesCount,
    int EventsInPeriodCount,
    decimal PlatformRevenueInPeriod,
    int NewAudienceSignupsInPeriod);
