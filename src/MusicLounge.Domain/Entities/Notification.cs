using MusicLounge.Domain.Enums;

namespace MusicLounge.Domain.Entities;

public sealed class Notification : Common.BaseEntity<int>
{
    public int UserId { get; set; }
    public NotificationType Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    // MLACP-489: bản tiếng Anh của Title/Body. null ở các dòng tạo trước MLACP-489 — đường đọc lùi về Title/Body tiếng
    // Việt, không bao giờ trả ô trống. Không backfill: câu cũ đã có nội suy (tên, số tiền) nên không dịch ngược được.
    public string? TitleEn { get; set; }
    public string? BodyEn { get; set; }
    public string? ReferenceType { get; set; }      // deep link: "show", "ticket", "settlement"
    public string? ReferenceId { get; set; }
    public bool IsRead { get; set; } = false;
    public DateTimeOffset? SentAt { get; set; }     // null = pending FCM delivery
    public DateTimeOffset CreatedAt { get; set; }

    public User User { get; set; } = null!;
}

/// <summary>
/// MLACP-489. Độ dài tối đa của cột — một nguồn cho cả cấu hình EF lẫn chỗ cắt câu ở NotificationService. Câu thông báo
/// có chèn nội dung do người gõ (ghi chú của Admin, mô tả khiếu nại) nên có thể vượt; vượt thì SQL Server ném lỗi lúc
/// lưu, mà nhiều chỗ tạo thông báo nằm trong transaction thanh toán.
/// </summary>
public static class NotificationLimits
{
    public const int TitleMaxLength = 255;
    public const int BodyMaxLength = 1000;
}
