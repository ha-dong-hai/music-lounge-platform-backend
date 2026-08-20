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
    // Monthly quota of AI-generated posters this package grants — only meaningful when
    // HasAiPoster is true. Resets each calendar month; only successful generations count against
    // it (a failed AI call is the platform's problem, not the Owner's, so it doesn't cost them a
    // poster — see GeneratePosterCommandHandler).
    public int MaxAiPostersPerMonth { get; set; } = 0;
    // Max panorama scenes an Owner can add to their venue's 360° virtual tour — 0 means the tour
    // feature is unavailable on this tier (still snapshotted like the other entitlements below, so
    // a later package edit can't shrink a tour an Owner already built mid-subscription).
    public int MaxTourScenes { get; set; } = 0;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<OwnerSubscription> Subscriptions { get; set; } = [];
}
