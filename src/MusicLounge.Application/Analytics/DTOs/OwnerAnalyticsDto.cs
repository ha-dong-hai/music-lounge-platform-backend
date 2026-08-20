namespace MusicLounge.Application.Analytics.DTOs;

public sealed record TopShowDto(
    int ShowId,
    string Name,
    DateTimeOffset ScheduledStart,
    string? MainPerformerName,
    int TicketsSold,
    int? TotalCapacity,
    decimal? AverageRating,
    decimal Revenue);

public sealed record RevenueMonthDto(
    int Year,
    int Month,
    decimal FnbRevenue,
    decimal OfflineTicketRevenue,
    decimal OnlineTicketRevenue);

public sealed record OwnerAnalyticsDto(
    int TotalShows,
    int UpcomingShows,
    int PastShows,
    int TotalTicketsSold,
    int OfflineTicketsSold,
    int OnlineTicketsSold,
    decimal TotalRevenue,
    decimal TicketRevenue,
    decimal FnbRevenue,
    decimal? AverageRating,
    int TotalRatings,
    int PendingArtistPayoutCount,
    decimal PendingArtistPayoutAmount,
    IReadOnlyList<RevenueMonthDto> RevenueTrend,
    IReadOnlyList<TopShowDto> TopShows);
