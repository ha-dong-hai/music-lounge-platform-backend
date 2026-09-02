using MusicLounge.Domain.Enums;

namespace MusicLounge.Domain.Entities;

// MLACP-222: bao cao vi pham tu nguoi dung cho noi dung DA hien thi (show/livestream/rating) —
// khac voi EventModeration (cong duyet AI truoc khi dang). Nhieu dong co the cung tro toi 1
// (TargetType, TargetId) — so dong Status=Open chinh la "so lan bao cao" dung de sap xep uu tien
// hang doi cua Admin.
public sealed class ContentReport : Common.BaseEntity<int>
{
    public ReportTargetType TargetType { get; set; }
    public int TargetId { get; set; }
    public int ReporterId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public ContentReportStatus Status { get; set; } = ContentReportStatus.Open;
    public DateTimeOffset CreatedAt { get; set; }

    public int? ResolvedByAdminId { get; set; }
    public string? ResolutionNote { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }

    public User Reporter { get; set; } = null!;
    public User? ResolvedByAdmin { get; set; }
}
