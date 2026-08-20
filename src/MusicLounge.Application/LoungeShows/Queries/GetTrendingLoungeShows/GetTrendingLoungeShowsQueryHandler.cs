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
        // Unlike SearchLoungeShowsQueryHandler/GetRecommendedLoungeShowsQueryHandler, this had no
        // clamp — a zero/negative Limit reaches TOP(0)/TOP(-1) in the repository, and an
        // unreasonably large one defeats the "small trending list" contract.
        var limit = Math.Clamp(request.Limit, 1, 50);
        var shows = await _showRepo.GetTrendingAsync(limit, request.City, ct);

        var wishlisted = _currentUser.IsAuthenticated
            ? await _showRepo.GetWishlistedShowIdsAsync(_currentUser.UserId, ct)
            : (IReadOnlySet<int>)new HashSet<int>();

        return shows.Select(s => s.ToListItemDto(wishlisted)).ToList();
    }
}
