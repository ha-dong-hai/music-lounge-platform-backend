using MusicLounge.Application.Common.Abstractions;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.Lounges.DTOs;

namespace MusicLounge.Application.Lounges.Queries.GetLounges;

// MLACP-502: Keyword tuy chon — tim theo ten phong tra.
// MLACP-521: ProvinceCode tuy chon — loc theo ma tinh chinh thuc (QD 19/2025).
public sealed record GetLoungesQuery(string? City, bool Mine, int Page, int PageSize, string? Keyword = null, string? ProvinceCode = null)
    : IQuery<PaginatedResult<LoungeListItemDto>>;
