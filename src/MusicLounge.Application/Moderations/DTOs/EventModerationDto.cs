namespace MusicLounge.Application.Moderations.DTOs;

public sealed record EventModerationDto(
    Guid Id,
    string TargetType,
    Guid TargetId,
    float? AiScore,
    string? RiskLevel,
    string? FlagReason,
    string? AiRecommendation,
    Guid? AdminId,
    string? AdminDecision,
    string? ReviewNote,
    DateTimeOffset CreatedAt,
    DateTimeOffset? SlaDeadline,
    DateTimeOffset? ReviewedAt
)
{
    /// <summary>MLACP-672: tên đọc được của đối tượng (buổi diễn, buổi phát, hạng vé, ảnh, cảnh 360°) thay cho mã — null khi đối tượng không còn tồn tại.</summary>
    public string? TargetName { get; init; }

    /// <summary>MLACP-692: ảnh của đối tượng (ảnh thư viện, cảnh 360) để Admin NHÌN THẤY thứ mình duyệt — null với loại khác
    /// hoặc khi nội dung đã bị xoá.</summary>
    public string? TargetImageUrl { get; init; }

    /// <summary>MLACP-692: phòng trà sở hữu ảnh/cảnh — Admin cần biết ảnh của ai.</summary>
    public string? LoungeName { get; init; }
}
