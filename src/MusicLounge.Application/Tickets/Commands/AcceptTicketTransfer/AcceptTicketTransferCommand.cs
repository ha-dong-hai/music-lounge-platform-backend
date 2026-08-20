using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Tickets.Commands.AcceptTicketTransfer;

public sealed record AcceptTicketTransferCommand(Guid TicketId) : ICommand;
