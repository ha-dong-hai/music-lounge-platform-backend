namespace MusicLounge.Application.Analytics.DTOs;

public sealed record DailyTicketSalesDto(
    DateOnly Date,
    int TicketsSold,
    decimal Revenue);

public sealed record TicketTierSalesDto(
    Guid TierId,
    string TierName,
    int TicketsSold,
    int? Capacity,
    decimal? SellThroughRate);

public sealed record TicketSalesTrendDto(
    Guid ShowId,
    string ShowName,
    int TotalTicketsSold,
    decimal TotalRevenue,
    IReadOnlyList<DailyTicketSalesDto> DailySales,
    IReadOnlyList<TicketTierSalesDto> ByTier);
