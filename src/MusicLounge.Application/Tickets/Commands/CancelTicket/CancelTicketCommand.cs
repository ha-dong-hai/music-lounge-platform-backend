using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Tickets.Commands.CancelTicket;

public sealed record CancelTicketCommand(Guid TicketId) : ICommand<int>;
