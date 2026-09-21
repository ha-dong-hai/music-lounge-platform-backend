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
    // MLACP-483: TRAN thoi thi chua du. Truoc day so con lai chi co trong cau tra loi cua chinh lan bam, nen chu phong
    // tra phai TIEU MOT LUOT de doc mot con so. Hai truong nay dem theo DUNG luat ma lenh tao poster dung (AiPosterQuota):
    // Succeeded/Queued/Rendering chiem mot suat, Failed/Expired thi tra lai luot.
    int AiPostersUsedThisMonth,
    int AiPostersRemainingThisMonth,
    int MaxTourScenesSnapshot,
    DateTimeOffset? CancelledAt); // MLACP-371: đã huỷ — gói vẫn dùng tới ExpiresAt, không gia hạn nữa
