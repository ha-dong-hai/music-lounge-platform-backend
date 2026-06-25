// CoreFlow: CF6 (Payment & Revenue)
// An Owner's active or historical subscription to a platform package.
// Snapshot fields preserve the package terms at the time of subscription (see D12).
using MusicLounge.Domain.Common;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Domain.Entities;

public class OwnerSubscription : BaseEntity<int>
{
    public int OwnerId { get; set; }
    public int PackageId { get; set; }
    public DateTime StartedAt { get; set; }
    // Extended by suspension_days when venue penalty is lifted
    public DateTime ExpiresAt { get; set; }
    public OwnerSubscriptionStatus Status { get; set; } = OwnerSubscriptionStatus.Active;
    public bool AutoRenew { get; set; } = false;
    public DateTime? CancelledAt { get; set; }
    // Snapshot of package limit at subscription time — unchanged even if package is later edited
    public int MaxTicketsPerEventSnapshot { get; set; }
    // Snapshot of AI poster feature at subscription time
    public bool HasAiPosterSnapshot { get; set; }
}
