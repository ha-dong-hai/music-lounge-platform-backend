using MusicLounge.Application.Common.Abstractions;
using MusicLounge.Application.Tickets.DTOs;

namespace MusicLounge.Application.Tickets.Commands.CheckInTicket;

public sealed record CheckInTicketCommand(string QrCode) : ICommand<TicketDetailDto>;
