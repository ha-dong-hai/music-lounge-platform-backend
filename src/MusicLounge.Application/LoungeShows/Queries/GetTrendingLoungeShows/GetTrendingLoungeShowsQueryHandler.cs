using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Common.Interfaces.Repositories;
using MusicLounge.Application.LoungeShows.DTOs;

namespace MusicLounge.Application.LoungeShows.Queries.GetTrendingLoungeShows;

internal sealed class GetTrendingLoungeShowsQueryHandler
    : IRequestHandler<GetTrendingLoungeShowsQuery, IReadOnlyList<LoungeShowListItemDto>>
{
    private readonly ILoungeShowRepository _showRepo;
    private readonly ICurrentUserService _currentUser;

    public GetTrendingLoungeShowsQueryHandler(
        ILoungeShowRepository showRepo,
        ICurrentUserService currentUser)
    {
        _showRepo = showRepo;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<LoungeShowListItemDto>> Handle(
        GetTrendingLoungeShowsQuery request, CancellationToken ct)
    {
        var shows = await _showRepo.GetTrendingAsync(request.Limit, request.City, ct);

        var wishlisted = _currentUser.IsAuthenticated
            ? await _showRepo.GetWishlistedShowIdsAsync(_currentUser.UserId, ct)
            : (IReadOnlySet<int>)new HashSet<int>();

        return shows.Select(s => s.ToListItemDto(wishlisted)).ToList();
    }
}
