// CoreFlow: CF1 (Event Management), CF2 (Event Discovery)
// Links a performer to a specific show with their role and schedule details.
// Junction table between MusicShow and Performer with additional detail fields.
using MusicLounge.Domain.Common;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Domain.Entities;

public class Performance : BaseEntity<int>
{
    public int ShowId { get; set; }
    public int PerformerId { get; set; }
    public PerformanceRole Role { get; set; } = PerformanceRole.Main;
    // Order in the show lineup — null if not scheduled
    public int? SetOrder { get; set; }
    // Planned start time for this performer's set
    public TimeOnly? SetTime { get; set; }
    // If false, donation button is hidden for this performer in this show
    public bool AcceptsDonation { get; set; } = true;
}
