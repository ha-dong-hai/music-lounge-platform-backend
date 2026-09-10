using MusicLounge.Application.Common.Abstractions;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.Settlements.DTOs;

namespace MusicLounge.Application.Settlements.Queries.GetSettlementsPendingReview;

public sealed record GetSettlementsPendingReviewQuery(int Page, int PageSize)
    : IQuery<PaginatedResult<SettlementReviewDto>>;
