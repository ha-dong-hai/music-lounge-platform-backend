using MusicLounge.Application.Analytics.DTOs;
using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Analytics.Queries.GetOwnerArtistDonationStats;

/// <param name="From">MLACP-659: đầu kỳ theo lúc VNPay xác nhận tiền ủng hộ (bao gồm). Null = mọi thời gian.</param>
/// <param name="To">MLACP-659: cuối kỳ (bao gồm).</param>
public sealed record GetOwnerArtistDonationStatsQuery(Guid LoungeId, DateTimeOffset? From = null, DateTimeOffset? To = null)
    : IQuery<OwnerArtistDonationReportDto>;
