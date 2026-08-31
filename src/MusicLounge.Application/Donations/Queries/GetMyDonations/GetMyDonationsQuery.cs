using MusicLounge.Application.Common.Abstractions;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.Donations.DTOs;

namespace MusicLounge.Application.Donations.Queries.GetMyDonations;

public sealed record GetMyDonationsQuery(int Page, int PageSize)
    : IQuery<PaginatedResult<MyDonationDto>>;
