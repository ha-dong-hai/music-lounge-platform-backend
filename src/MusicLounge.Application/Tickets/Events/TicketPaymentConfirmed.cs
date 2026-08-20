using MediatR;

namespace MusicLounge.Application.Tickets.Events;

public record TicketPaymentConfirmed(
    int PaymentId,
    int UserId,
    int OwnerId,
    Guid[] TicketIds,
    int? LivestreamId) : INotification;
