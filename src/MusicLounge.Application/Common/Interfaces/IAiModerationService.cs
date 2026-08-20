namespace MusicLounge.Application.Common.Interfaces;

// Fail-open by design: EventModeration.AiScore/RiskLevel/FlagReason/AiRecommendation are a
// priority hint for Admin's review queue (GetPendingModerations already orders by AiScore
// descending), never an auto-decision — an unscored item just sits at normal priority and still
// gets reviewed within its SlaDeadline like anything else. So a null result (no API key
// configured, the vendor is down, a malformed response) is a normal, expected outcome, not an
// error the caller needs to handle specially.
public interface IAiModerationService
{
    Task<AiModerationResult?> ScoreAsync(string content, CancellationToken ct = default);
}

public sealed record AiModerationResult(
    float Score,
    string RiskLevel,
    string? FlagReason,
    string Recommendation);
