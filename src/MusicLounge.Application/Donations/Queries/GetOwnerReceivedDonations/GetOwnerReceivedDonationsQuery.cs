using MusicLounge.Application.Common.Abstractions;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.Donations.DTOs;

namespace MusicLounge.Application.Donations.Queries.GetOwnerReceivedDonations;

public sealed record GetOwnerReceivedDonationsQuery(int Page, int PageSize)
    : IQuery<PaginatedResult<PendingDonationDto>>;
