using MusicLounge.Application.Common.Abstractions;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.Performers.DTOs;

namespace MusicLounge.Application.Performers.Queries.GetPerformers;

public sealed record GetPerformersQuery(string? Search, int Page, int PageSize)
    : IQuery<PaginatedResult<PerformerDto>>;
