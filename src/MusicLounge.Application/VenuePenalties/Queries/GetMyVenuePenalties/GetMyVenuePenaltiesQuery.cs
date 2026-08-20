using MusicLounge.Application.Common.Abstractions;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.VenuePenalties.DTOs;

namespace MusicLounge.Application.VenuePenalties.Queries.GetMyVenuePenalties;

public sealed record GetMyVenuePenaltiesQuery(int Page, int PageSize)
    : IQuery<PaginatedResult<VenuePenaltyDto>>;
