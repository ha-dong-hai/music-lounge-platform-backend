namespace MusicLounge.Application.Tickets.DTOs;

public sealed record TicketPriceStatDto(
    int TierId,
    string TierName,
    int PriceId,
    string PriceName,
    decimal UnitPrice,
    int QuantitySold,
    decimal Revenue,
    int CheckedInCount);

public sealed record ShowTicketStatsDto(
    int ShowId,
    string ShowName,
    int TotalTicketsSold,
    decimal TotalRevenue,
    int TotalCheckedIn,
    IReadOnlyList<TicketPriceStatDto> ByPrice);
