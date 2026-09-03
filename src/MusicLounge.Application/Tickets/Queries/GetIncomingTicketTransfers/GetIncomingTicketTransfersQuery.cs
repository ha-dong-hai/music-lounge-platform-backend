using MusicLounge.Application.Common.Abstractions;
using MusicLounge.Application.Tickets.DTOs;

namespace MusicLounge.Application.Tickets.Queries.GetIncomingTicketTransfers;

public sealed record GetIncomingTicketTransfersQuery : IQuery<IReadOnlyList<IncomingTicketTransferDto>>;
