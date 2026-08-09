namespace MusicLounge.Application.Subscriptions.DTOs;

public sealed record MySubscriptionDto(
    int Id,
    int PackageId,
    string PackageName,
    DateTimeOffset StartedAt,
    DateTimeOffset ExpiresAt,
    string Status,
    bool AutoRenew,
    int MaxTicketsPerEventSnapshot,
    bool HasAiPosterSnapshot,
    int MaxAiPostersPerMonthSnapshot);
