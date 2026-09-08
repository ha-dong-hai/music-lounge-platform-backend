using MediatR;
using MusicLounge.Application.Common;
using MusicLounge.Application.Common.Constants;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Common.Interfaces.Repositories;
using MusicLounge.Application.Lounges.DTOs;
using MusicLounge.Domain.Enums;
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

        // BR-01 (MLACP-307). Lọc phòng trà chưa duyệt khỏi danh sách mà vẫn phục vụ trang chi tiết
        // của nó thì coi như chưa lọc: đường dẫn /lounges/{id} đoán được, và một địa điểm chưa ai
        // xác minh vẫn có trang giới thiệu công khai. Chính chủ và Admin thì vẫn xem được — họ cần
        // thấy đúng hồ sơ đang chờ duyệt đó.
        if (!Enum.TryParse<LoungeStatus>(lounge.Status, out var status)
            || !VenueLifecycle.IsPubliclyVisible(status))
        {
            var isOwnerOrAdmin = _currentUser.IsAuthenticated
                && (lounge.OwnerId == _currentUser.UserId || _currentUser.Role == Roles.Admin);

            // 404 chứ không phải 403: với người ngoài, phòng trà chưa duyệt là thứ không tồn tại.
            // Trả 403 thì chính câu trả lời đó xác nhận có một phòng trà mang Id này.
            if (!isOwnerOrAdmin)
                throw new NotFoundException("Lounge", request.LoungeId);
        }

        // Enrich with caller's follow status if authenticated
        if (_currentUser.IsAuthenticated)
        {
            var isFollowing = await _repo.IsFollowingAsync(request.LoungeId, _currentUser.UserId, ct);
            return lounge with { IsFollowing = isFollowing };
        }

        return lounge;
    }
}
