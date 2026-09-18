namespace MusicLounge.Application.Analytics.DTOs;

/// <param name="TotalVenues">
/// MỌI phòng trà đã đăng ký, ở mọi trạng thái — kể cả đang chờ duyệt, bị từ chối, bị đình chỉ, bị cấm. Không phải số
/// phòng trà đang hoạt động (xem <paramref name="OperatingVenues"/>). Giữ nguyên nghĩa cũ để không làm vỡ client.
/// </param>
/// <param name="OperatingVenues">
/// MLACP-452: số phòng trà đang được phép hoạt động, theo đúng <c>VenueLifecycle.Operating</c> (Approved + Warned) — cùng
/// định nghĩa với <c>ActiveVenuesCount</c> của <c>/analytics/admin-overview</c> và danh sách phòng trà công khai. Trước đây
/// ba endpoint cho ba con số (3 / 2 / 2) mà không trường nào nói mình đếm gì.
/// </param>
/// <param name="VenuesByStatus">
/// MLACP-452: số phòng trà theo từng trạng thái, đủ mọi khoá (kể cả 0) để client không phải đoán khoá nào có mặt.
/// Tổng các giá trị bằng <paramref name="TotalVenues"/>.
/// </param>
public sealed record PlatformAnalyticsDto(
    int TotalVenues,
    int TotalPublishedShows,
    int TotalUsers,
    int TotalTicketsSold,
    decimal TotalGrossMerchandiseValue,
    decimal TotalDonationVolume,
    int PendingModerationsCount,
    int OperatingVenues = 0,
    IReadOnlyDictionary<string, int>? VenuesByStatus = null);
