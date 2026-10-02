using MediatR;

namespace MusicLounge.Application.Tickets.Events;

public record TicketPaymentConfirmed(
    Guid PaymentId,
    Guid UserId,
    Guid OwnerId,
    Guid[] TicketIds,
    Guid? LivestreamId,
    Guid ShowId) : INotification;
