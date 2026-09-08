using MusicLounge.Application.Analytics.DTOs;
using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Analytics.Queries.GetOwnerRevenueReport;

public sealed record GetOwnerRevenueReportQuery(
    int LoungeId,
    DateTimeOffset? From,
    DateTimeOffset? To
) : IQuery<OwnerRevenueReportDto>;
