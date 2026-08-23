using MusicLounge.Application.Common.Abstractions;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.Lounges.DTOs;

namespace MusicLounge.Application.Lounges.Queries.GetLounges;

public sealed record GetLoungesQuery(string? City, bool Mine, int Page, int PageSize)
    : IQuery<PaginatedResult<LoungeListItemDto>>;
