using MusicLounge.Application.Analytics.DTOs;
using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Analytics.Queries.GetOwnerAnalytics;

public sealed record GetOwnerAnalyticsQuery(int LoungeId) : IQuery<OwnerAnalyticsDto>;
