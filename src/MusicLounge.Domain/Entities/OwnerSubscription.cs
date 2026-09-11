using MusicLounge.Domain.Enums;

namespace MusicLounge.Domain.Entities;

public sealed class OwnerSubscription : Common.BaseEntity<int>
{
    public int OwnerId { get; set; }
    public int PackageId { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }   // extended by suspension_days when venue penalized
    public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Active;
    // MLACP-371: chủ huỷ gói → gói VẪN Active tới hết ExpiresAt, CancelledAt ghi lúc huỷ. Gói bị dừng hẳn (khoá
    // vĩnh viễn, đổi sang gói khác, hoặc gói đã huỷ tới hạn) thì Status = Cancelled.
    public DateTimeOffset? CancelledAt { get; set; }

    // MLACP-371: số tiền đã trả cho khoảng thời gian của gói này (lần mua đầu + các lần gia hạn sớm + phần giá trị
    // quy đổi khi đổi gói) — để tính giá trị còn lại khi đổi gói. Null: gói có từ trước khi có cột này; nơi dùng
    // lấy giá niêm yết của gói thay thế.
    public decimal? AmountPaid { get; set; }

    // D12: Snapshot at subscription time — package changes after do not affect active sub
    public int MaxTicketsPerEventSnapshot { get; set; }
    public bool HasAiPosterSnapshot { get; set; }
    public int MaxAiPostersPerMonthSnapshot { get; set; }
    public int MaxTourScenesSnapshot { get; set; }

    public User Owner { get; set; } = null!;
    public SubscriptionPackage Package { get; set; } = null!;
}
