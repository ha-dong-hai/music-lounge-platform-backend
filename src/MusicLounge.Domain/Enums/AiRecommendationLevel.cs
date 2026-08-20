// CoreFlow: CF1 (Event Management), CF4 (Livestream Participation)
// AI-generated suggestion to Admin for the moderation queue.
// This is a RECOMMENDATION only — Admin always makes the final decision (see D11).
namespace MusicLounge.Domain.Enums;

public enum AiRecommendationLevel
{
    // AI score is low — content appears safe, suggest approving
    SuggestApprove = 1,
    // AI score is medium — content needs human review
    NeedsReview = 2,
    // AI score is high — content likely violates policy, suggest rejecting
    SuggestReject = 3
}
