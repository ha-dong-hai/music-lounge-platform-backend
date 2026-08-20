using MusicLounge.Application.Common.Abstractions;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.Donations.DTOs;

namespace MusicLounge.Application.Donations.Queries.GetPendingDonations;

public sealed record GetPendingDonationsQuery(int Page, int PageSize)
    : IQuery<PaginatedResult<PendingDonationDto>>;
