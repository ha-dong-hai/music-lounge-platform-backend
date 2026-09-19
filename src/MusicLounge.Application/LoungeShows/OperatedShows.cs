using MusicLounge.Application.Common.Constants;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Common.Interfaces.Repositories;
using MusicLounge.Application.Common.Models;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.LoungeShows;

/// <summary>
/// MLACP-466. "Buổi hòa nhạc của tôi" nghĩa là gì, tuỳ vai trò — một quy tắc, dùng chung cho mọi danh sách kiểu này.
///
/// <b>Lỗi đã có:</b> danh sách này lọc theo NGƯỜI SỞ HỮU phòng trà. Nhân viên không sở hữu buổi nào, nên nhân viên luôn
/// nhận danh sách rỗng — trong khi các thao tác livestream lại cho phép nhân viên (<c>RequireVenueOperator</c>). Nhân
/// viên vào được đúng trang livestream, và trang đó trống: "chưa có buổi diễn Online" dù phòng trà có. Có quyền thao
/// tác mà không có cách nào biết thao tác trên buổi nào.
///
/// Quy tắc lấy nguyên từ <see cref="Common.VenueOperatorAccess"/> — chỗ đã định nghĩa ai vận hành được phòng trà nào:
/// nhân viên vận hành đúng phòng trà ghi trong claim <c>lounge_id</c> của token (mỗi nhân viên một phòng trà tại một thời
/// điểm); chủ phòng trà vận hành các phòng trà mình sở hữu. Không tự đặt quy tắc mới ở đây.
/// </summary>
internal static class OperatedShows
{
    public static Task<PaginatedResult<LoungeShow>> QueryAsync(
        ILoungeShowRepository repo, ICurrentUserService currentUser,
        int page, int pageSize, LoungeShowSortBy sortBy, LoungeShowStatus? status, CancellationToken ct)
    {
        if (currentUser.Role == Roles.Staff)
        {
            // Nhân viên chưa được phân công phòng trà nào thì không có buổi nào để vận hành — trả rỗng, không phải lỗi.
            return currentUser.LoungeId is int loungeId
                ? repo.GetForOperatedLoungeAsync(loungeId, page, pageSize, sortBy, status, ct)
                : Task.FromResult(new PaginatedResult<LoungeShow>([], page, pageSize, 0));
        }

        return repo.GetMineAsync(currentUser.UserId, page, pageSize, sortBy, status, ct);
    }
}
