// CoreFlow: CF1 (Event Management), CF4 (Livestream Participation)
// AI-assessed risk level of content being moderated.
// Determines which queue Admin sees first — Critical is shown at the top.
namespace MusicLounge.Domain.Enums;

public enum RiskLevel
{
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}
