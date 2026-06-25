// CoreFlow: CF1 (Event Management), CF3 (Ticket Booking)
// Maps a user with role Staff to a specific venue they are authorized to operate in.
// JWT role alone is not enough — staff permissions are always venue-scoped via this table (see D6).
using MusicLounge.Domain.Common;

namespace MusicLounge.Domain.Entities;

public class LoungeStaff : BaseEntity<int>
{
    public int LoungeId { get; set; }
    public int UserId { get; set; }
    public int AssignedBy { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime AssignedAt { get; set; }
    // Set when Owner deactivates this staff assignment
    public DateTime? DeactivatedAt { get; set; }
}
