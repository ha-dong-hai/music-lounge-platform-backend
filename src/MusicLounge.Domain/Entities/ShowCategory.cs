// CoreFlow: CF1 (Event Management), CF2 (Event Discovery)
// Admin-managed categories for classifying shows (e.g. "Jazz Night", "Acoustic Session").
using MusicLounge.Domain.Common;

namespace MusicLounge.Domain.Entities;

public class ShowCategory : BaseEntity<int>
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
}
