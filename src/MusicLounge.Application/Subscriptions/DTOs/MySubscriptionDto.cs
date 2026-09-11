namespace MusicLounge.Application.Subscriptions.DTOs;

public sealed record MySubscriptionDto(
    int Id,
    int PackageId,
    string PackageName,
    DateTimeOffset StartedAt,
    DateTimeOffset ExpiresAt,
    string Status,
    int MaxTicketsPerEventSnapshot,
    bool HasAiPosterSnapshot,
    int MaxAiPostersPerMonthSnapshot,
    int MaxTourScenesSnapshot,
    DateTimeOffset? CancelledAt); // MLACP-371: đã huỷ — gói vẫn dùng tới ExpiresAt, không gia hạn nữa
