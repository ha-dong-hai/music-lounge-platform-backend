using MusicLounge.Application.Analytics.DTOs;
using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Analytics.Queries.GetTicketSalesTrend;

public sealed record GetTicketSalesTrendQuery(int ShowId) : IQuery<TicketSalesTrendDto>;
