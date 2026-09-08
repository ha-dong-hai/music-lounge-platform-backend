using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Common.Interfaces.Repositories;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.LoungeShows.DTOs;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Exceptions;

namespace MusicLounge.Application.LoungeShows.Queries.GetLoungeShowsByPerformer;

internal sealed class GetLoungeShowsByPerformerQueryHandler
    : IRequestHandler<GetLoungeShowsByPerformerQuery, PerformerDetailDto>
{
    private readonly ILoungeShowRepository _showRepo;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public GetLoungeShowsByPerformerQueryHandler(
        ILoungeShowRepository showRepo, IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _showRepo = showRepo;
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<PerformerDetailDto> Handle(
        GetLoungeShowsByPerformerQuery request, CancellationToken ct)
    {
        var performer = await _uow.Repository<Performer, int>().GetByIdAsync(request.PerformerId, ct)
            ?? throw new NotFoundException(nameof(Performer), request.PerformerId);

        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var shows = await _showRepo.GetByPerformerAsync(
            request.PerformerId, request.IncludeEnded, page, pageSize, ct);

        var wishlisted = _currentUser.IsAuthenticated
            ? await _showRepo.GetWishlistedShowIdsAsync(_currentUser.UserId, ct)
            : (IReadOnlySet<int>)new HashSet<int>();

        return performer.ToDetailDto(new PaginatedResult<LoungeShowListItemDto>(
            shows.Items.Select(s => s.ToListItemDto(wishlisted)).ToList(),
            shows.Page, shows.PageSize, shows.TotalCount));
    }
}
