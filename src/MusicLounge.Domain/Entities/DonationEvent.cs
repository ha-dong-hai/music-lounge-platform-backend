using MusicLounge.Domain.Enums;

namespace MusicLounge.Domain.Entities;

// MLACP-363: IMMUTABLE — chỉ INSERT, không bao giờ UPDATE hay DELETE (cùng quy ước với SystemConfigHistory).
// Mỗi sự kiện mang mã băm của sự kiện trước nó trong cùng khoản donate, nên sửa hay xoá một dòng bất kỳ
// làm đứt chuỗi và bị phát hiện khi kiểm lại.
public sealed class DonationEvent : Common.BaseEntity<long>
{
    public int DonationId { get; set; }
    public int Sequence { get; set; }                   // 1, 2, 3… trong phạm vi một khoản donate
    public DonationEventType EventType { get; set; }
    public DateTimeOffset OccurredAt { get; set; }      // giờ máy chủ, cắt về mili-giây
    public int? ActorUserId { get; set; }               // null = hệ thống / cổng thanh toán
    public decimal? Amount { get; set; }
    public string? Reference { get; set; }              // mã giao dịch VNPay, mã chuyển khoản, mã quyết toán
    public string? EvidenceUrl { get; set; }
    public string? EvidenceSha256 { get; set; }         // băm nội dung file bằng chứng lúc nộp
    public string? Detail { get; set; }
    public string? PreviousHash { get; set; }
    public string Hash { get; set; } = string.Empty;
}
