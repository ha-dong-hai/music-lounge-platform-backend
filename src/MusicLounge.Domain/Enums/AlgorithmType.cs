// CoreFlow: CF2 (Event Discovery)
// Algorithm used to generate an AI recommendation record.
// See AI scoring formula in complete_reference.md section 7.
namespace MusicLounge.Domain.Enums;

public enum AlgorithmType
{
    // Genre + mood + atmosphere matching only
    ContentBased = 1,
    // ALS collaborative filtering on user_event_scores matrix only
    Collaborative = 2,
    // Combined: content×0.5 + collab×0.3 + custom×0.2 (default for active users)
    Hybrid = 3
}
