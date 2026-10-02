namespace MusicLounge.Application.Tickets.DTOs;

public sealed record PaymentInitiationDto(
    Guid PaymentId,
    string OrderId,
    decimal Amount,
    string PaymentUrl,
    Guid[] TicketIds);
