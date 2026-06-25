// CoreFlow: CF5 (Interaction & Feedback)
// Current state of a penalty — tracks whether it is being appealed or has been reviewed.
namespace MusicLounge.Domain.Enums;

public enum PenaltyStatus
{
    Active = 1,
    // Venue has filed an appeal — under Admin review
    Appealed = 2,
    // Admin reviewed appeal and reversed the penalty
    Overturned = 3,
    // Admin reviewed appeal and upheld the penalty
    Upheld = 4,
    // Suspension period has ended naturally
    Expired = 5
}
