using MusicLounge.Domain.Enums;

namespace MusicLounge.Domain.Entities;

// D12: Immutable when owners are subscribed — create new version instead of editing
public sealed class SubscriptionPackage : Common.BaseEntity<int>
{
    public string Name { get; set; } = string.Empty;       // Basic / Pro / Premium
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public SubscriptionBillingCycle BillingCycle { get; set; }
    public int MaxTicketsPerEvent { get; set; }
    public bool HasAiPoster { get; set; } = false;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<OwnerSubscription> Subscriptions { get; set; } = [];
}
