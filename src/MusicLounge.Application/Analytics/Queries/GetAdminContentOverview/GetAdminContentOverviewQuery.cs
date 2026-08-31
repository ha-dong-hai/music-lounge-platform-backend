using MusicLounge.Application.Analytics.DTOs;
using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Analytics.Queries.GetAdminContentOverview;

public sealed record GetAdminContentOverviewQuery : IQuery<AdminContentOverviewDto>;
