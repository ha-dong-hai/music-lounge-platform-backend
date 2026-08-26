using MusicLounge.Application.Common.Abstractions;
using MusicLounge.Application.Tickets.DTOs;

namespace MusicLounge.Application.Tickets.Queries.GetTicketDetail;

public sealed record GetTicketDetailQuery(Guid TicketId) : IQuery<TicketDetailDto>;
