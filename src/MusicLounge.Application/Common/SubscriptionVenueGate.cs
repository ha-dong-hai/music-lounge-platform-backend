using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.Exceptions;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Application.Common;

/// <summary>
/// MLACP-376 — chặn chủ mua / gia hạn / đổi gói dịch vụ khi phòng trà đang bị phạt (Suspended/Locked).
///
/// <para>Mọi hành động cần tới gói (tạo buổi diễn, bán vé, nhận donation) đều đã bị <see cref="VenueLifecycle.
/// CanOperate"/> chặn ở cấp phòng trà (MLACP-354) — nhưng không có gì chặn chính việc MUA gói, nên nền tảng thu
/// tiền của chủ cho một dịch vụ không dùng được ngay lúc mua.</para>
///
/// <para>Không chặn <see cref="LoungeStatus.Pending"/>/<see cref="LoungeStatus.Rejected"/>: đó là chưa từng
/// được duyệt hoạt động, không phải hình phạt — chủ mới cần mua gói trước khi phòng trà được duyệt, và Owner
/// chưa có phòng trà nào (MLACP-374: tối đa một phòng trà) cũng là luồng hợp lệ tương tự, không bị chặn.
/// <see cref="LoungeStatus.Warned"/> nằm trong <see cref="VenueLifecycle.Operating"/> nên cũng không bị chặn —
/// cảnh cáo là một vết ghi lại, không phải lệnh dừng.</para>
/// </summary>
public static class SubscriptionVenueGate
{
    public static async Task EnsureNotPenalizedAsync(IUnitOfWork uow, int ownerId, CancellationToken ct)
    {
        var lounge = (await uow.Repository<MusicLoungeEntity, int>().FindAsync(l => l.OwnerId == ownerId, ct))
            .FirstOrDefault();
        if (lounge is null || lounge.Status is not (LoungeStatus.Suspended or LoungeStatus.Locked)) return;

        var action = lounge.Status == LoungeStatus.Locked ? "bị khoá vĩnh viễn" : "bị tạm khoá";
        throw new ConflictException(
            $"Phòng trà của bạn đang {action} do vi phạm — chưa thể mua, gia hạn hay đổi gói dịch vụ lúc này.");
    }
}
