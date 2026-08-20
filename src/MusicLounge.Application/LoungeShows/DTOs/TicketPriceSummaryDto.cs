using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.LoungeShows.DTOs;

public sealed record TicketPriceSummaryDto(
    int Id,
    string Name,
    decimal Price,
    int? Quota,
    DateTimeOffset SaleStart,
    DateTimeOffset SaleEnd,
    PurchaseChannel PurchaseChannel,
    int? AvailableSlots);
