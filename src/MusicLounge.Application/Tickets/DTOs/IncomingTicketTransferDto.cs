namespace MusicLounge.Application.Tickets.DTOs;

public sealed record IncomingTicketTransferDto(
    Guid TicketId,
    string ShowName,
    string LoungeName,
    DateTimeOffset ShowScheduledStart,
    string TierName,
    string PriceName,
    decimal PricePaid,
    DateTimeOffset InitiatedAt);
