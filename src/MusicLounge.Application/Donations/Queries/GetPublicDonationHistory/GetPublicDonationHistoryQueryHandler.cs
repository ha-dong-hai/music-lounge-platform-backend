using MediatR;
using MusicLounge.Application.Common.Interfaces.Repositories;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.Donations.DTOs;

namespace MusicLounge.Application.Donations.Queries.GetPublicDonationHistory;

internal sealed class GetPublicDonationHistoryQueryHandler
    : IRequestHandler<GetPublicDonationHistoryQuery, PaginatedResult<PublicDonationDto>>
{
    private readonly IDonationRepository _repo;

    public GetPublicDonationHistoryQueryHandler(IDonationRepository repo) => _repo = repo;

    public async Task<PaginatedResult<PublicDonationDto>> Handle(
        GetPublicDonationHistoryQuery request, CancellationToken ct)
    {
        var page = Math.Max(1, request.Page);
        var size = Math.Clamp(request.PageSize, 1, 100);
        return await _repo.GetPublicHistoryByPerformerAsync(request.PerformerId, page, size, ct);
    }
}
