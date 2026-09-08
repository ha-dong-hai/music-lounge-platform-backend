namespace MusicLounge.Domain.Entities;

// Giới hạn số phiên xem đồng thời cho vé Livestream PPV — nền tảng dùng Mux HLS công khai (không
// DRM/signed-URL xoay vòng) nên app server không thể thu hồi 1 phiên đang phát giữa chừng; cơ chế
// này chỉ CHẶN PHIÊN MỚI vượt hạn mức (xem ConfigKeys.LivestreamMaxConcurrentSessionsPerTicket).
// Phiên không heartbeat quá timeout (ConfigKeys.LivestreamHeartbeatTimeoutSeconds) tự động không
// còn tính là "đang hoạt động" khi đếm — lọc theo LastHeartbeatAt tại điểm kiểm tra, không cần job
// dọn dẹp riêng (cùng pattern với TicketHold.ExpiresAt).
public sealed class LivestreamViewingSession : Common.BaseEntity<int>
{
    public Guid TicketId { get; set; }
    public int LivestreamId { get; set; }
    public string SessionId { get; set; } = string.Empty;
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset LastHeartbeatAt { get; set; }

    public Ticket Ticket { get; set; } = null!;
    public Livestream Livestream { get; set; } = null!;
}
