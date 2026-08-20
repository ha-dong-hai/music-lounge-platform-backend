using MusicLounge.Application.Common.Abstractions;
using MusicLounge.Application.Tickets.DTOs;

namespace MusicLounge.Application.Tickets.Commands.SellWalkInTicket;

public sealed record SellWalkInTicketCommand(int PriceId, int Quantity) : ICommand<WalkInSaleResultDto>;
