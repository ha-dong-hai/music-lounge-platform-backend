using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Tickets.Commands.InitiateTicketTransfer;

public sealed record InitiateTicketTransferCommand(Guid TicketId, string RecipientEmail) : ICommand;
