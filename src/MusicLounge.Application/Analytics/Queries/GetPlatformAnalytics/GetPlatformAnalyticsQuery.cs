using MusicLounge.Application.Analytics.DTOs;
using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Analytics.Queries.GetPlatformAnalytics;

public sealed record GetPlatformAnalyticsQuery : IQuery<PlatformAnalyticsDto>;
