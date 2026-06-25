// CoreFlow: CF6 (Payment & Revenue), CF7 (Analytics & Reporting)
// IMMUTABLE audit trail for all system config changes — append only, never UPDATE or DELETE.
// EffectiveFrom must be >= NOW() — config changes cannot be backdated.
// Note field is mandatory — Admin must provide a reason for every financial config change (see D9).
using MusicLounge.Domain.Common;

namespace MusicLounge.Domain.Entities;

public class SystemConfigHistory : BaseEntity<long>
{
    public string ConfigKey { get; set; } = string.Empty;
    public string? OldValue { get; set; }
    public string NewValue { get; set; } = string.Empty;
    // When the new config value takes effect — must be in the future
    public DateTimeOffset EffectiveFrom { get; set; }
    public int ChangedBy { get; set; }
    public DateTimeOffset ChangedAt { get; set; }
    // Mandatory reason — required for financial config changes
    public string Note { get; set; } = string.Empty;
}
