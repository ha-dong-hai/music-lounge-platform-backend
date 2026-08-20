using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Tickets.Commands.CancelTicketTransfer;

public sealed record CancelTicketTransferCommand(Guid TicketId) : ICommand;
