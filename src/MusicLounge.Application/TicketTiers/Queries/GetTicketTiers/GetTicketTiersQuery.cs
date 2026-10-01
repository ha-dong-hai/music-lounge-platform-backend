using MusicLounge.Application.Common.Abstractions;
using MusicLounge.Application.LoungeShows.DTOs;

namespace MusicLounge.Application.TicketTiers.Queries.GetTicketTiers;

public sealed record GetTicketTiersQuery(Guid ShowId) : IQuery<IReadOnlyList<TicketTierSummaryDto>>;
