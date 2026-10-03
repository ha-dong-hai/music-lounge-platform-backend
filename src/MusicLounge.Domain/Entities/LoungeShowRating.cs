using MusicLounge.Domain.Enums;

namespace MusicLounge.Domain.Entities;

// AuditableEntity — UpdatedBy/UpdatedAt is genuinely new info RemoveRatingCommandHandler never
// recorded before: which Admin removed this rating and when (only IsRemoved/RemovedReason existed,
// no actor/timestamp). CreatedBy is redundant with UserId on the one creation path, but harmless.
public sealed class LoungeShowRating : Common.AuditableEntity<Guid>
{
    public Guid? UserId { get; set; }
    public Guid LoungeShowId { get; set; }
    public int Score { get; set; }
    public string? Comment { get; set; }
    public bool IsRemoved { get; set; } = false;
    public string? RemovedReason { get; set; }

    // MLACP-574: AI chấm LỜI BÌNH sau khi gửi (ScoreRatingWithAiJob). Trước đó lời bình công khai ngay, không qua bước
    // kiểm nội dung nào — chỉ có Admin gỡ tay và báo cáo từ người dùng.
    // Cả ba trường null = chưa chấm (không có lời bình, AI chưa chạy, hoặc AI không trả lời — fail-open).
    public float? AiScore { get; set; }
    public ModerationRiskLevel? AiRiskLevel { get; set; }
    public string? AiFlagReason { get; set; }

    /// <summary>
    /// MLACP-574: thời điểm PHẦN CHỮ bị ẩn tạm vì AI đánh giá rủi ro cao, chờ Admin quyết ở hàng đợi báo cáo vi phạm.
    /// Chỉ ẩn chữ — <see cref="Score"/> vẫn tính vào điểm trung bình và phân bố sao: AI bắt nhầm không được phép làm
    /// lệch điểm của phòng trà. Admin "Bỏ qua" → về null (hiện lại); Admin "Gỡ" → <see cref="IsRemoved"/> như cũ.
    /// </summary>
    public DateTimeOffset? CommentHiddenAt { get; set; }

    /// <summary>
    /// Lời bình cho mọi nơi CÔNG KHAI đọc. MỘT chỗ duy nhất áp luật ẩn tạm — nơi công khai nào đọc thẳng
    /// <see cref="Comment"/> là để lọt lời đang chờ duyệt. Hàng đợi Admin và bản xuất dữ liệu của chính chủ đọc
    /// <see cref="Comment"/> (họ phải thấy nguyên văn).
    /// </summary>
    public string? PublicComment => CommentHiddenAt is null ? Comment : null;

    public User? User { get; set; }
    public LoungeShow LoungeShow { get; set; } = null!;
}
