// CoreFlow: CF6 (Payment & Revenue)
// Two-stage settlement release schedule (see D3 in complete_reference.md).
namespace MusicLounge.Domain.Enums;

public enum SettlementReleaseType
{
    // Pre-show release: percentage released 3 days before scheduled_start
    Partial70 = 1,
    // Post-show release: remaining amount released 3 days after actual_end
    Final30 = 2
}
