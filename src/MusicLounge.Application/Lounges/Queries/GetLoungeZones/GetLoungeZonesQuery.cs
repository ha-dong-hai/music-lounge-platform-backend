using MusicLounge.Application.Common.Abstractions;
using MusicLounge.Application.Lounges.DTOs;

namespace MusicLounge.Application.Lounges.Queries.GetLoungeZones;

public sealed record GetLoungeZonesQuery(int LoungeId, bool ActiveOnly = false)
    : IQuery<IReadOnlyList<SeatingZoneDto>>;
