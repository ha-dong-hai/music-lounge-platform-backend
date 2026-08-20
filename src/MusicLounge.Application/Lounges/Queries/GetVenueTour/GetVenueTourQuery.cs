using MusicLounge.Application.Common.Abstractions;
using MusicLounge.Application.Lounges.DTOs;

namespace MusicLounge.Application.Lounges.Queries.GetVenueTour;

public sealed record GetVenueTourQuery(int LoungeId) : IQuery<VenueTourDto>;
