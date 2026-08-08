using MusicLounge.Application.Common.Abstractions;
using MusicLounge.Application.Tickets.DTOs;

namespace MusicLounge.Application.Tickets.Queries.GetTicketByQr;

public sealed record GetTicketByQrQuery(string QrCode) : IQuery<TicketDetailDto>;
