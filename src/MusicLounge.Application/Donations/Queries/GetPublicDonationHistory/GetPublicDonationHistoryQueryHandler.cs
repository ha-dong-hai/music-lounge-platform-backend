using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Common.Interfaces.Repositories;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.Donations.DTOs;

namespace MusicLounge.Application.Donations.Queries.GetPublicDonationHistory;

internal sealed class GetPublicDonationHistoryQueryHandler
    : IRequestHandler<GetPublicDonationHistoryQuery, PaginatedResult<PublicDonationDto>>
{
    private readonly IDonationRepository _repo;
    private readonly IUnitOfWork _uow;
    private readonly ISystemConfigService _config;

    public GetPublicDonationHistoryQueryHandler(IDonationRepository repo, IUnitOfWork uow, ISystemConfigService config)
    {
        _repo = repo;
        _uow = uow;
        _config = config;
    }

    public async Task<PaginatedResult<PublicDonationDto>> Handle(
        GetPublicDonationHistoryQuery request, CancellationToken ct)
    {
        var page = Math.Max(1, request.Page);
        var size = Math.Clamp(request.PageSize, 1, 100);
        var rows = await _repo.GetPublicHistoryByPerformerAsync(request.PerformerId, page, size, ct);

        // MLACP-365: dong thoi gian, phan bo tien va phan hoi cua nghe si — cung nguon voi cac job.
        var items = await PublicDonationStatement.BuildAsync(_uow, _config, rows.Items, ct);
        return new PaginatedResult<PublicDonationDto>(items, rows.Page, rows.PageSize, rows.TotalCount);
    }
}
