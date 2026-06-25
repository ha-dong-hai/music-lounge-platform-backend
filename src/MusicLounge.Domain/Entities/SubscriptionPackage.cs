// CoreFlow: CF6 (Payment & Revenue)
// Platform subscription tiers available to venue Owners (e.g. Basic, Pro, Premium).
// Immutable once an Owner has an active subscription — create a new version instead of editing (see D12).
using MusicLounge.Domain.Common;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Domain.Entities;

public class SubscriptionPackage : BaseEntity<int>
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public SubscriptionBillingCycle BillingCycle { get; set; }
    public int MaxTicketsPerEvent { get; set; }
    // AI poster generation feature — only available on higher tiers
    public bool HasAiPoster { get; set; } = false;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
}
