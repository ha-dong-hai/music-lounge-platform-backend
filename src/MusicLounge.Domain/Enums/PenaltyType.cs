// CoreFlow: CF5 (Interaction & Feedback), CF6 (Payment & Revenue)
// Severity of the penalty issued by Admin against a venue.
namespace MusicLounge.Domain.Enums;

public enum PenaltyType
{
    // Formal warning — no operational impact yet
    Warning = 1,
    // Venue temporarily blocked — subscription extended by suspension_days
    Suspension = 2,
    // Permanent ban — venue locked forever
    Ban = 3
}
