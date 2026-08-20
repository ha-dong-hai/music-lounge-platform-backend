namespace MusicLounge.Application.Tickets.DTOs;

public sealed record ShowOrderDto(
    Guid TicketId,
    string? BuyerName,
    string? BuyerEmail,
    string TierName,
    string PriceName,
    decimal PricePaid,
    string Status,
    string PurchaseChannel,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CheckedInAt);
