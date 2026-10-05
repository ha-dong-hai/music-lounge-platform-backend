namespace MusicLounge.Application.Subscriptions.DTOs;

public sealed record SubscriptionPackageDto(
    Guid Id,
    string Name,
    string? Description,
    decimal Price,
    string BillingCycle,
    int MaxTicketsPerEvent,
    bool HasAiPoster,
    int MaxAiPostersPerMonth,
    int MaxTourScenes,
    bool IsActive,
    // MLACP-677: số chủ phòng trà đang dùng gói (còn hạn) — chỉ trả cho Admin (null với người khác), để Admin thấy trước
    // ẩn gói thì ảnh hưởng ai.
    int? ActiveSubscriberCount = null);
