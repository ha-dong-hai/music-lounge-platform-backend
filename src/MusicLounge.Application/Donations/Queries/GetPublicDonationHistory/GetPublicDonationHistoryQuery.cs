using MusicLounge.Application.Common.Abstractions;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.Donations.DTOs;

namespace MusicLounge.Application.Donations.Queries.GetPublicDonationHistory;

public sealed record GetPublicDonationHistoryQuery(
    int PerformerId,
    int Page = 1,
    int PageSize = 20
) : IQuery<PaginatedResult<PublicDonationDto>>;
