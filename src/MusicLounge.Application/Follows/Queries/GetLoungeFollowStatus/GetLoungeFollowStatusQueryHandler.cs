using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Common.Interfaces.Repositories;
using MusicLounge.Application.Follows.DTOs;

namespace MusicLounge.Application.Follows.Queries.GetLoungeFollowStatus;

/// <summary>
/// MLACP-503. Trái tim "Theo dõi" trên 5 màn (chi tiết/danh sách phòng trà, chi tiết buổi diễn...) được tính bằng cách
/// tải trang đầu của danh sách đang theo dõi (/follows/lounges, kẹp 50) rồi tìm trong đó — theo dõi quá 50 phòng là trái
/// tim của phòng thứ 51 trở đi luôn tắt. Giờ hỏi thẳng theo id: một id cho màn chi tiết, nhiều id cho cả trang danh sách
/// trong MỘT lời gọi. Chi tiết phòng trà (GET /lounges/{id}) vẫn tự trả isFollowing như trước.
///
/// <para>Phòng trà không tồn tại thì trả isFollowing=false, không 404: câu hỏi là "tôi có theo dõi không", và trả lời
/// khác nhau theo việc phòng trà có tồn tại sẽ biến endpoint này thành công cụ dò id phòng trà chưa duyệt.</para>
/// </summary>
internal sealed class GetLoungeFollowStatusQueryHandler
    : IRequestHandler<GetLoungeFollowStatusQuery, IReadOnlyList<LoungeFollowStatusDto>>
{
    private readonly IFollowRepository _repo;
    private readonly ICurrentUserService _currentUser;

    public GetLoungeFollowStatusQueryHandler(IFollowRepository repo, ICurrentUserService currentUser)
    {
        _repo = repo;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<LoungeFollowStatusDto>> Handle(GetLoungeFollowStatusQuery request, CancellationToken ct)
    {
        var ids = request.LoungeIds.Distinct().ToList();
        var dangTheoDoi = await _repo.GetFollowedAmongAsync(_currentUser.UserId, ids, ct);
        return ids.Select(id => new LoungeFollowStatusDto(id, dangTheoDoi.Contains(id))).ToList();
    }
}
