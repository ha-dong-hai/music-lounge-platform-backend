using MusicLounge.Application.Analytics.DTOs;
using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Analytics.Queries.GetAiRecommendationPerformance;

public sealed record GetAiRecommendationPerformanceQuery(
    DateTimeOffset? From,
    DateTimeOffset? To
) : IQuery<AiRecommendationPerformanceDto>;
