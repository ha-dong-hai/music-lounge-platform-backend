namespace MusicLounge.Application.Moderations.DTOs;

// MLACP-78: rieng cho danh sach show dang cho duyet — khac EventModerationDto (chung cho moi
// TargetType, chi co TargetId tho, khong du de Admin nhan ra day la su kien nao ma khong bam vao
// tung dong). Gop thong tin show (ten/phong tra/ngay dien) voi tin hieu AI moderation trong 1 dong.
public sealed record PendingLoungeShowDto(
    int ShowId,
    string Name,
    string? CoverImageUrl,
    string LoungeName,
    DateTimeOffset ScheduledStart,
    string Format,
    float? AiScore,
    string? RiskLevel,
    string? FlagReason,
    DateTimeOffset CreatedAt,
    DateTimeOffset? SlaDeadline);
