using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Common.Interfaces.Repositories;
using MusicLounge.Application.LoungeShows.DTOs;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Exceptions;

namespace MusicLounge.Application.LoungeShows.Queries.GetSimilarLoungeShows;

internal sealed class GetSimilarLoungeShowsQueryHandler
    : IRequestHandler<GetSimilarLoungeShowsQuery, IReadOnlyList<LoungeShowListItemDto>>
{
    // DONE WHEN: "tối đa 6 sự kiện" — quy định cố định của tính năng, không phải tham số client
    // được phép tuỳ chỉnh.
    private const int MaxResults = 6;

    private readonly ILoungeShowRepository _showRepo;
    private readonly ICurrentUserService _currentUser;

    public GetSimilarLoungeShowsQueryHandler(ILoungeShowRepository showRepo, ICurrentUserService currentUser)
    {
        _showRepo = showRepo;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<LoungeShowListItemDto>> Handle(
        GetSimilarLoungeShowsQuery request, CancellationToken ct)
    {
        var show = await _showRepo.GetByIdWithDetailsAsync(request.ShowId, ct)
            ?? throw new NotFoundException(nameof(LoungeShow), request.ShowId);

        var genreIds = show.Genres.Select(g => g.GenreId).ToList();
        var similar = await _showRepo.GetSimilarAsync(show.Id, show.LoungeId, genreIds, MaxResults, ct);

        var wishlisted = _currentUser.IsAuthenticated
            ? await _showRepo.GetWishlistedShowIdsAsync(_currentUser.UserId, ct)
            : (IReadOnlySet<int>)new HashSet<int>();

        return similar.Select(s => s.ToListItemDto(wishlisted)).ToList();
    }
}
