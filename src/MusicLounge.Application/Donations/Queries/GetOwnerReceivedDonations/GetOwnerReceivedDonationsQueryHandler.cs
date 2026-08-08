using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Common.Interfaces.Repositories;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.Donations.DTOs;

namespace MusicLounge.Application.Donations.Queries.GetOwnerReceivedDonations;

internal sealed class GetOwnerReceivedDonationsQueryHandler
    : IRequestHandler<GetOwnerReceivedDonationsQuery, PaginatedResult<PendingDonationDto>>
{
    private readonly IDonationRepository _repo;
    private readonly ICurrentUserService _currentUser;

    public GetOwnerReceivedDonationsQueryHandler(IDonationRepository repo, ICurrentUserService currentUser)
    {
        _repo = repo;
        _currentUser = currentUser;
    }

    public async Task<PaginatedResult<PendingDonationDto>> Handle(
        GetOwnerReceivedDonationsQuery request, CancellationToken ct)
    {
        var page = Math.Max(1, request.Page);
        var size = Math.Clamp(request.PageSize, 1, 50);
        return await _repo.GetOwnerReceivedAwaitingPayoutAsync(_currentUser.UserId, page, size, ct);
    }
}
