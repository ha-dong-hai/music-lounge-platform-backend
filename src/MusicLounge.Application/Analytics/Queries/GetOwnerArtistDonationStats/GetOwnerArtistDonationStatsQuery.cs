using MusicLounge.Application.Analytics.DTOs;
using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Analytics.Queries.GetOwnerArtistDonationStats;

public sealed record GetOwnerArtistDonationStatsQuery(Guid LoungeId) : IQuery<OwnerArtistDonationReportDto>;
