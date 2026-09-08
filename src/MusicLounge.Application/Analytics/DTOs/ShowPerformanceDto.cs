namespace MusicLounge.Application.Analytics.DTOs;

public sealed record ShowPerformanceDto(
    int ShowId,
    string ShowName,
    int TotalPageViews,
    int UniqueViewers,
    int TicketsSold,
    int TicketsCheckedIn,
    decimal CheckInRate,
    int UniquePurchasers,
    decimal ConversionRate,
    int LiveViewers);
