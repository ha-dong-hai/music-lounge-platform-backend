using MusicLounge.Application.Analytics.DTOs;
using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Analytics.Queries.GetAdminPlatformOverview;

public sealed record GetAdminPlatformOverviewQuery(
    DateTimeOffset? From,
    DateTimeOffset? To
) : IQuery<AdminPlatformOverviewDto>;
