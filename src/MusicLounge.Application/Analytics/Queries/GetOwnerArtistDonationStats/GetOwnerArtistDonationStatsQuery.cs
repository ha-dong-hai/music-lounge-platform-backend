using MusicLounge.Application.Analytics.DTOs;
using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Analytics.Queries.GetOwnerArtistDonationStats;

public sealed record GetOwnerArtistDonationStatsQuery(int LoungeId) : IQuery<OwnerArtistDonationReportDto>;
