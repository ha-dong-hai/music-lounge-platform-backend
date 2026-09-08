using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Common.Interfaces.Repositories;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.Lounges.DTOs;
using MusicLounge.Domain.Exceptions;

namespace MusicLounge.Application.Lounges.Queries.GetLounges;

internal sealed class GetLoungesQueryHandler
    : IRequestHandler<GetLoungesQuery, PaginatedResult<LoungeListItemDto>>
{
    private readonly ILoungeRepository _repo;
    private readonly ICurrentUserService _currentUser;

    public GetLoungesQueryHandler(ILoungeRepository repo, ICurrentUserService currentUser)
    {
        _repo = repo;
        _currentUser = currentUser;
    }

    public async Task<PaginatedResult<LoungeListItemDto>> Handle(
        GetLoungesQuery request, CancellationToken ct)
    {
        var page = Math.Max(1, request.Page);
        var size = Math.Clamp(request.PageSize, 1, 50);

        int? ownerId = null;
        if (request.Mine)
        {
            if (!_currentUser.IsAuthenticated)
                throw new UnauthorizedException("Vui lòng đăng nhập để xem phòng trà của bạn.");
            ownerId = _currentUser.UserId;
        }

        // Owner xem phòng trà của chính mình thì thấy cả hồ sơ đang chờ duyệt lẫn hồ sơ bị từ
        // chối; người ngoài chỉ thấy phòng trà đã được duyệt (BR-01, MLACP-307).
        return await _repo.GetAllAsync(request.City, ownerId, request.Mine, page, size, ct);
    }
}
