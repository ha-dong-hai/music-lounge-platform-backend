using MusicLounge.Application.Common.Abstractions;
using MusicLounge.Application.Tickets.DTOs;

namespace MusicLounge.Application.Tickets.Commands.PurchaseTicket;

public sealed record PurchaseTicketCommand(int HoldId, string ClientIpAddress)
    : ICommand<PaymentInitiationDto>;
