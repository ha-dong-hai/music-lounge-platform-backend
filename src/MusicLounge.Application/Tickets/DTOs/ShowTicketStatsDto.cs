namespace MusicLounge.Application.Tickets.DTOs;

public sealed record TicketPriceStatDto(
    Guid TierId,
    string TierName,
    Guid PriceId,
    string PriceName,
    decimal UnitPrice,
    int QuantitySold,
    decimal Revenue,
    int CheckedInCount);

public sealed record ShowTicketStatsDto(
    Guid ShowId,
    string ShowName,
    int TotalTicketsSold,
    decimal TotalRevenue,
    int TotalCheckedIn,
    IReadOnlyList<TicketPriceStatDto> ByPrice);
