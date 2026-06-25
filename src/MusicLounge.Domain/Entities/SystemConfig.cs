// CoreFlow: CF6 (Payment & Revenue), CF7 (Analytics & Reporting)
// Key-value store for all business parameters — commission rates, hold minutes, settlement thresholds.
// No business logic value should be hardcoded in application code (see D9).
// Financial config changes require Admin password confirmation and notify affected Owners.
// All changes are audited in SystemConfigHistory (immutable).
using MusicLounge.Domain.Common;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Domain.Entities;

public class SystemConfig : BaseEntity<int>
{
    public string ConfigKey { get; set; } = string.Empty;
    // Always stored as string — parsed using DataType field
    public string ConfigValue { get; set; } = string.Empty;
    public SystemConfigDataType DataType { get; set; }
    public string? Description { get; set; }
    // Nullable — SET NULL when updater account is deleted
    public int? UpdatedBy { get; set; }
    public DateTime UpdatedAt { get; set; }
}
