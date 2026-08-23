using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Common.Interfaces.Repositories;
using MusicLounge.Application.Lounges.DTOs;
using MusicLounge.Domain.Exceptions;

namespace MusicLounge.Application.Lounges.Queries.GetLoungeDetail;

internal sealed class GetLoungeDetailQueryHandler
    : IRequestHandler<GetLoungeDetailQuery, LoungeDetailDto>
{
    private readonly ILoungeRepository _repo;
    private readonly ICurrentUserService _currentUser;

    public GetLoungeDetailQueryHandler(ILoungeRepository repo, ICurrentUserService currentUser)
    {
        _repo = repo;
        _currentUser = currentUser;
    }

    public async Task<LoungeDetailDto> Handle(GetLoungeDetailQuery request, CancellationToken ct)
    {
        var lounge = await _repo.GetByIdAsync(request.LoungeId, ct)
            ?? throw new NotFoundException("Lounge", request.LoungeId);

        // Enrich with caller's follow status if authenticated
        if (_currentUser.IsAuthenticated)
        {
            var isFollowing = await _repo.IsFollowingAsync(request.LoungeId, _currentUser.UserId, ct);
            return lounge with { IsFollowing = isFollowing };
        }

        return lounge;
    }
}
