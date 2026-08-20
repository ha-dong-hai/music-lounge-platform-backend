// CoreFlow: CF5 (Interaction & Feedback)
// Final outcome of a penalty appeal reviewed by Admin.
// No "pending" state — use PenaltyStatus.Appealed while under review.
namespace MusicLounge.Domain.Enums;

public enum AppealResult
{
    Overturned = 1,
    Upheld = 2
}
