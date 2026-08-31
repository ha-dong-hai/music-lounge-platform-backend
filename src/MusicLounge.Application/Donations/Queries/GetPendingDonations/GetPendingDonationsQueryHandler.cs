using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Common.Interfaces.Repositories;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.Donations.DTOs;

namespace MusicLounge.Application.Donations.Queries.GetPendingDonations;

internal sealed class GetPendingDonationsQueryHandler
    : IRequestHandler<GetPendingDonationsQuery, PaginatedResult<PendingDonationDto>>
{
    private readonly IDonationRepository _repo;
    private readonly ICurrentUserService _currentUser;
    private readonly ISystemConfigService _config;

    public GetPendingDonationsQueryHandler(
        IDonationRepository repo, ICurrentUserService currentUser, ISystemConfigService config)
    {
        _repo = repo;
        _currentUser = currentUser;
        _config = config;
    }

    public async Task<PaginatedResult<PendingDonationDto>> Handle(
        GetPendingDonationsQuery request, CancellationToken ct)
    {
        var page = Math.Max(1, request.Page);
        var size = Math.Clamp(request.PageSize, 1, 50);
        var performerShareRate = await _config.GetDecimalAsync(ConfigKeys.DonationPerformerShareRate, 0.88m, ct);
        return await _repo.GetPendingForOwnerAsync(_currentUser.UserId, performerShareRate, page, size, ct);
    }
}
