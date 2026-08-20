using MusicLounge.Domain.Enums;

namespace MusicLounge.Domain.Entities;

public sealed class OwnerSubscription : Common.BaseEntity<int>
{
    public int OwnerId { get; set; }
    public int PackageId { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }   // extended by suspension_days when venue penalized
    public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Active;
    public DateTimeOffset? CancelledAt { get; set; }

    // D12: Snapshot at subscription time — package changes after do not affect active sub
    public int MaxTicketsPerEventSnapshot { get; set; }
    public bool HasAiPosterSnapshot { get; set; }
    public int MaxAiPostersPerMonthSnapshot { get; set; }
    public int MaxTourScenesSnapshot { get; set; }

    public User Owner { get; set; } = null!;
    public SubscriptionPackage Package { get; set; } = null!;
}
