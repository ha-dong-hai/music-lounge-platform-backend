using MusicLounge.Application.Analytics.DTOs;
using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Analytics.Queries.GetAudienceEngagementStats;

public sealed record GetAudienceEngagementStatsQuery(
    DateTimeOffset? From,
    DateTimeOffset? To
) : IQuery<AudienceEngagementStatsDto>;
