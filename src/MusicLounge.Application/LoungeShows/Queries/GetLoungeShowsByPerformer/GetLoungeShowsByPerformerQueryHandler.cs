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
    private readonly IRepository<Performer, int> _performerRepo;
    private readonly ICurrentUserService _currentUser;

    public GetLoungeShowsByPerformerQueryHandler(
        ILoungeShowRepository showRepo,
        IRepository<Performer, int> performerRepo,
        ICurrentUserService currentUser)
    {
        _showRepo = showRepo;
        _performerRepo = performerRepo;
        _currentUser = currentUser;
    }

    public async Task<PerformerDetailDto> Handle(
        GetLoungeShowsByPerformerQuery request, CancellationToken ct)
    {
        var performer = await _performerRepo.GetByIdAsync(request.PerformerId, ct)
            ?? throw new NotFoundException(nameof(Performer), request.PerformerId);

        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var showsResult = await _showRepo.GetByPerformerAsync(
            request.PerformerId, request.IncludeEnded,
            page, pageSize, ct);

        var wishlisted = _currentUser.IsAuthenticated
            ? await _showRepo.GetWishlistedShowIdsAsync(_currentUser.UserId, ct)
            : (IReadOnlySet<int>)new HashSet<int>();

        var showItems = showsResult.Items.Select(s => s.ToListItemDto(wishlisted)).ToList();
        var paginatedShows = new PaginatedResult<LoungeShowListItemDto>(
            showItems, showsResult.Page, showsResult.PageSize, showsResult.TotalCount);

        return performer.ToDetailDto(paginatedShows);
    }
}
