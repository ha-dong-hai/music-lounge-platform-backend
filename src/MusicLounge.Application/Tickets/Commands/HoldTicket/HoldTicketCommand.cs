using MusicLounge.Application.Common.Abstractions;
using MusicLounge.Application.Tickets.DTOs;

namespace MusicLounge.Application.Tickets.Commands.HoldTicket;

public sealed record HoldTicketCommand(int PriceId, int Quantity) : ICommand<HoldTicketResultDto>;
