using MusicLounge.Application.Common.Abstractions;
using MusicLounge.Application.Tickets.DTOs;

namespace MusicLounge.Application.Tickets.Queries.GetShowTicketStats;

public sealed record GetShowTicketStatsQuery(int ShowId) : IQuery<ShowTicketStatsDto>;
