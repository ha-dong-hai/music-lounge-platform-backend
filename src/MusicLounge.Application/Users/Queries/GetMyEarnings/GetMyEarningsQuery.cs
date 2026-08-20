using MusicLounge.Application.Common.Abstractions;
using MusicLounge.Application.Users.DTOs;

namespace MusicLounge.Application.Users.Queries.GetMyEarnings;

public sealed record GetMyEarningsQuery : IQuery<EarningsSummaryDto>;
