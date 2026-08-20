namespace MusicLounge.Application.Tickets.DTOs;

public sealed record WalkInSaleResultDto(
    int PaymentId,
    decimal Amount,
    Guid[] TicketIds);
