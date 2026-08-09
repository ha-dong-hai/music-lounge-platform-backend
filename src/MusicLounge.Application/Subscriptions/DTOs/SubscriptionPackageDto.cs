namespace MusicLounge.Application.Subscriptions.DTOs;

public sealed record SubscriptionPackageDto(
    int Id,
    string Name,
    string? Description,
    decimal Price,
    string BillingCycle,
    int MaxTicketsPerEvent,
    bool HasAiPoster,
    int MaxAiPostersPerMonth,
    int MaxTourScenes,
    bool IsActive);
