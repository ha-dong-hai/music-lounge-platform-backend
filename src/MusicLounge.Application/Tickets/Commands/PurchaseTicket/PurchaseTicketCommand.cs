using MusicLounge.Application.Common.Abstractions;
using MusicLounge.Application.Tickets.DTOs;

namespace MusicLounge.Application.Tickets.Commands.PurchaseTicket;

public sealed record PurchaseTicketCommand(Guid HoldId, string ClientIpAddress)
    : ICommand<PaymentInitiationDto>;
