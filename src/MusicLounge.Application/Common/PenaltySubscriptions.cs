using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.Common;

/// <summary>
/// MLACP-369 — gói dịch vụ của chủ phòng trà khi phòng trà bị khoá vĩnh viễn, và khi lệnh khoá đó được huỷ.
///
/// <para><b>Khoá vĩnh viễn không hoàn phí gói.</b> Trước đây job ghi một bút toán "hoàn tiền theo tỉ lệ"
/// (Platform nợ / Gateway có) nhưng không gọi VNPay, không tạo yêu cầu hoàn, không báo chủ — sổ cái nói tiền
/// đã ra, không đồng nào chuyển đi. Theo thông lệ, nền tảng chấm dứt tài khoản vì vi phạm thì không hoàn
/// phí gói (Shopify: "no refunds are issued for any remaining period post-cancellation"; Squarespace chấm
/// dứt vì vi phạm "without any refunds"); BLĐS 2015 Điều 428: bên đơn phương chấm dứt vì bên kia vi phạm
/// nghiêm trọng không phải bồi thường.</para>
///
/// <para><b>Lệnh khoá bị huỷ thì trả lại thời gian.</b> Phòng trà bị khoá oan được kích hoạt lại gói với
/// đúng phần thời gian còn lại lúc bị khoá — cùng cách đã bù cho tạm khoá (cộng ngày), không cần Admin xử
/// lý tay.</para>
/// </summary>
public static class PenaltySubscriptions
{
    /// <summary>
    /// Dừng gói khi lệnh khoá vĩnh viễn có hiệu lực. Ghi <c>CancelledAt</c> đúng bằng lúc lệnh được áp — đó
    /// là cách duy nhất để khi lệnh bị huỷ còn biết gói nào đã bị dừng vì nó.
    /// </summary>
    public static void StopOnBan(OwnerSubscription? activePlan, DateTimeOffset appliedAt)
    {
        if (activePlan is null) return;
        activePlan.Status = SubscriptionStatus.Cancelled;
        activePlan.CancelledAt = appliedAt;
    }

    /// <summary>
    /// Lệnh khoá vĩnh viễn đã áp bị huỷ: trả lại đúng phần thời gian gói còn lại lúc bị khoá, tính từ bây
    /// giờ. Nếu chủ đã mua gói khác đang hoạt động thì cộng vào gói đó — mỗi chủ chỉ được có một gói Active.
    /// Trả về gói nhận lại thời gian, hoặc null khi không có gì để trả.
    /// </summary>
    public static OwnerSubscription? RestoreAfterBanLifted(
        IReadOnlyCollection<OwnerSubscription> ownerSubscriptions, VenuePenalty ban, DateTimeOffset now)
    {
        if (ban.PenaltyType != PenaltyType.Ban || ban.AppliedAt is not { } appliedAt) return null;

        var stopped = ownerSubscriptions.FirstOrDefault(
            s => s.Status == SubscriptionStatus.Cancelled && s.CancelledAt == appliedAt);
        if (stopped is null) return null;

        var left = stopped.ExpiresAt - appliedAt;
        if (left <= TimeSpan.Zero) return null;

        var active = ownerSubscriptions
            .Where(s => s.Status == SubscriptionStatus.Active)
            .OrderByDescending(s => s.ExpiresAt)
            .FirstOrDefault();
        if (active is not null)
        {
            active.ExpiresAt = (active.ExpiresAt > now ? active.ExpiresAt : now) + left;
            return active;
        }

        stopped.Status = SubscriptionStatus.Active;
        stopped.CancelledAt = null;
        stopped.ExpiresAt = now + left;
        return stopped;
    }
}
