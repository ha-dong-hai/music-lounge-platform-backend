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
}
