using MusicLounge.Domain.Common;

namespace MusicLounge.Domain.Entities;

public class AiRecommendation : BaseEntity<int>
{
    public int UserId { get; set; }
    public int ShowId { get; set; }
    // content_based / collaborative / hybrid
    public string Algorithm { get; set; } = string.Empty;
    // Nullable — only computed when algorithm includes content-based scoring
    public decimal? ContentScore { get; set; }
    // Nullable — only computed when algorithm includes collaborative filtering
    public decimal? CollabScore { get; set; }
    // Nullable — only computed when venue has custom criteria configured
    public decimal? CustomScore { get; set; }
    // Weighted combination: content×0.5 + collab×0.3 + custom×0.2
    public decimal FinalScore { get; set; }
    // Explainability text shown to user: "Vì bạn thích Jazz..."
    public string? Reason { get; set; }
    public DateTime CreatedAt { get; set; }
    // Cache TTL — recommendation is stale after this datetime (default 6 hours)
    public DateTime ExpiresAt { get; set; }
}
