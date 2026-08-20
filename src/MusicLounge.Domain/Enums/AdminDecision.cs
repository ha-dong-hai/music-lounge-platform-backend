// CoreFlow: CF1 (Event Management), CF4 (Livestream Participation)
// Final decision made by Admin on a moderation record.
// Only this field has legal authority — AI recommendation is advisory only (see D11).
namespace MusicLounge.Domain.Enums;

public enum AdminDecision
{
    Approved = 1,
    Rejected = 2,
    // Force-stopped an active livestream for policy violation
    Terminated = 3
}
