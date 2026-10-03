using MusicLounge.Domain.Enums;

namespace MusicLounge.Domain.Entities;

// MLACP-222: bao cao vi pham tu nguoi dung cho noi dung DA hien thi (show/livestream/rating) —
// khac voi EventModeration (cong duyet AI truoc khi dang). Nhieu dong co the cung tro toi 1
// (TargetType, TargetId) — so dong Status=Open chinh la "so lan bao cao" dung de sap xep uu tien
// hang doi cua Admin.
public sealed class ContentReport : Common.BaseEntity<Guid>
{
    public ReportTargetType TargetType { get; set; }
    public Guid TargetId { get; set; }
    // MLACP-574: null = báo cáo do HỆ THỐNG tạo (AI gắn cờ một lời bình rủi ro cao). Dùng chung hàng đợi Admin với báo
    // cáo của người dùng — cùng SLA, cùng hai hành động Gỡ / Bỏ qua — thay vì dựng một hàng đợi thứ hai.
    public Guid? ReporterId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public ContentReportStatus Status { get; set; } = ContentReportStatus.Open;
    public DateTimeOffset CreatedAt { get; set; }

    public Guid? ResolvedByAdminId { get; set; }
    public string? ResolutionNote { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }

    public User? Reporter { get; set; }
    public User? ResolvedByAdmin { get; set; }
}
