using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Application.Auth;

/// <summary>
/// MLACP-449. Phòng trà gắn vào token (claim <c>lounge_id</c>) — một nguồn cho cả bốn luồng cấp token: đăng nhập, đăng nhập
/// Google, làm mới token, xác thực email. Trước đây cùng một khối tính toán được chép nguyên ở bốn nơi, và chỉ tính cho Staff.
///
/// <para>Chủ phòng trà trước đây nhận <c>null</c>, dù phòng trà của họ luôn xác định được: hệ thống ép đúng một chủ — một
/// phòng trà ở cả chỉ mục duy nhất trên <c>music_lounges.OwnerId</c> lẫn <c>CreateLoungeCommandHandler</c>. Thiếu claim, frontend
/// phải tự truyền <c>loungeId</c> ở mọi endpoint của chủ.</para>
///
/// <para>Claim chỉ nói "phòng trà của người này là phòng trà nào", KHÔNG phải quyền vận hành: phòng trà đang chờ duyệt hay bị
/// đình chỉ vẫn là của chủ đó, và việc có được thao tác hay không do các cổng riêng quyết định. Mọi chỗ dùng
/// <c>ICurrentUserService.LoungeId</c> để cho quyền "nhân viên phòng trà" phải kiểm kèm <c>Role == Staff</c>.</para>
///
/// <para>Chủ tạo phòng trà sau khi đăng nhập thì token cũ chưa có claim — lần làm mới token kế tiếp sẽ có.</para>
/// </summary>
public static class TokenLounge
{
    public static async Task<int?> ResolveAsync(IUnitOfWork uow, User user, CancellationToken ct)
    {
        switch (user.Role)
        {
            case UserRole.Staff:
                var assignments = await uow.Repository<LoungeStaff, int>()
                    .FindAsync(s => s.UserId == user.Id && s.IsActive, ct);
                return assignments.FirstOrDefault()?.LoungeId;

            case UserRole.Owner:
                var owned = await uow.Repository<MusicLoungeEntity, int>()
                    .FindAsync(l => l.OwnerId == user.Id, ct);
                return owned.FirstOrDefault()?.Id;

            default:
                return null;
        }
    }
}
