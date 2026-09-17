using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.Lounges;

/// <summary>
/// MLACP-436. Quy tắc dùng chung khi tạo cảnh tour 360 — thêm trực tiếp (AddVenueTourSceneCommandHandler), ghép ảnh
/// (StitchVenueTourSceneCommandHandler) và job ghép ảnh (StitchVenueTourSceneJob). Trước đây mỗi nơi tự chép một bản
/// đoạn chọn gói đang hiệu lực, và job thì không kiểm lại gì.
/// </summary>
public static class VenueTourRules
{
    /// <summary>
    /// Khoá theo phòng trà cho mọi đoạn "đếm rồi ghi" của tour (IAsyncKeyedLock, MLACP-396: trong command có
    /// transaction thì giữ tới lúc commit). Không có khoá, hai yêu cầu đồng thời cùng đếm thấy còn chỗ rồi cùng thêm.
    /// </summary>
    public static string LockKey(int loungeId) => $"venue-tour:{loungeId}";

    /// <summary>Số cảnh tối đa theo gói đang hiệu lực (snapshot lúc đăng ký — D12). 0 = không có gói hỗ trợ tour.</summary>
    public static int MaxScenes(IEnumerable<OwnerSubscription> subscriptions, DateTimeOffset now)
        => subscriptions
            .Where(s => s.Status == SubscriptionStatus.Active && s.ExpiresAt > now)
            .OrderByDescending(s => s.StartedAt)
            .FirstOrDefault()?.MaxTourScenesSnapshot ?? 0;

    /// <summary>Null nếu còn chỗ; ngược lại là thông báo cho chủ phòng trà.</summary>
    public static string? QuotaViolation(int existingSceneCount, int maxScenes)
        => existingSceneCount < maxScenes
            ? null
            : maxScenes == 0
                ? "Gói subscription hiện tại không hỗ trợ tour ảo 360° — vui lòng nâng cấp gói."
                : $"Tour đã đạt giới hạn {maxScenes} scene của gói subscription hiện tại.";

    /// <summary>
    /// Vị trí cho cảnh thêm vào cuối: lớn nhất hiện có + 1. Trước đây lấy SỐ cảnh — xoá một cảnh ở giữa rồi thêm cảnh
    /// mới thì hai cảnh trùng vị trí (0,2 + mới = 2) và thứ tự hiển thị không xác định.
    /// </summary>
    public static int NextOrderIndex(IEnumerable<VenueTourScene> scenes)
        => (scenes.Select(s => (int?)s.OrderIndex).Max() + 1) ?? 0;
}
