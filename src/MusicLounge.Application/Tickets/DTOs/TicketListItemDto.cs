using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.Tickets.DTOs;

public sealed record TicketListItemDto(
    Guid Id,
    int ShowId,
    string ShowName,
    string LoungeName,
    string LoungeCity,
    DateTimeOffset ShowScheduledStart,
    string TierName,
    string PriceName,
    decimal PricePaid,
    AccessType AccessType,
    TicketStatus Status,
    string? QrCode,
    DateTimeOffset PurchasedAt,
    bool HasPendingTransfer);
