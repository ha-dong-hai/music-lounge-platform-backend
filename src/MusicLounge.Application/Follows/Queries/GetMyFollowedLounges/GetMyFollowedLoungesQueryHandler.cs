using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Common.Interfaces.Repositories;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.Follows.DTOs;

namespace MusicLounge.Application.Follows.Queries.GetMyFollowedLounges;

internal sealed class GetMyFollowedLoungesQueryHandler
    : IRequestHandler<GetMyFollowedLoungesQuery, PaginatedResult<FollowedLoungeDto>>
{
    private readonly IFollowRepository _repo;
    private readonly ICurrentUserService _currentUser;

    public GetMyFollowedLoungesQueryHandler(IFollowRepository repo, ICurrentUserService currentUser)
    {
        _repo = repo;
        _currentUser = currentUser;
    }

    public async Task<PaginatedResult<FollowedLoungeDto>> Handle(
        GetMyFollowedLoungesQuery request, CancellationToken ct)
    {
        var page = Math.Max(1, request.Page);
        var size = Math.Clamp(request.PageSize, 1, 50);
        return await _repo.GetFollowedLoungesByUserAsync(_currentUser.UserId, page, size, ct);
    }
}
