using MusicLounge.Application.Common.Abstractions;
using MusicLounge.Application.Donations.DTOs;

namespace MusicLounge.Application.Donations.Queries.GetOwnerDonationHistory;

public sealed record GetOwnerDonationHistoryQuery(
    DateTimeOffset? From,
    DateTimeOffset? To,
    int Page = 1,
    int PageSize = 20
) : IQuery<OwnerDonationHistorySummaryDto>;
