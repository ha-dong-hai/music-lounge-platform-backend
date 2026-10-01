using MusicLounge.Application.Analytics.DTOs;
using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Analytics.Queries.GetDemandForecast;

public sealed record GetDemandForecastQuery(Guid ShowId) : IQuery<DemandForecastDto>;
