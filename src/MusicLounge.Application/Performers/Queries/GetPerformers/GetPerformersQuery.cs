using MusicLounge.Application.Common.Abstractions;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.Performers.DTOs;

namespace MusicLounge.Application.Performers.Queries.GetPerformers;

// MLACP-501: CreatedByMe tuy chon — chi lay nghe si do nguoi goi tao (CreatedByUserId).
public sealed record GetPerformersQuery(string? Search, int Page, int PageSize, bool CreatedByMe = false)
    : IQuery<PaginatedResult<PerformerDto>>;
