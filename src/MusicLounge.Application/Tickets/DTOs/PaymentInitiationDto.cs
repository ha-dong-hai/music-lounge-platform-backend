namespace MusicLounge.Application.Tickets.DTOs;

public sealed record PaymentInitiationDto(
    int PaymentId,
    string OrderId,
    decimal Amount,
    string PaymentUrl,
    Guid[] TicketIds);
