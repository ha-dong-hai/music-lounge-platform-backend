using MusicLounge.Application.Common.Abstractions;
using MusicLounge.Application.LoungeShows.DTOs;

namespace MusicLounge.Application.LoungeShows.Queries.GetShowSeatingMap;

public sealed record GetShowSeatingMapQuery(int ShowId) : IQuery<SeatingMapDto>;
