// CoreFlow: CF1 (Event Management), CF3 (Ticket Booking)
// Physical seating area within a venue — exists at venue level and reused across many shows.
// e.g. "VIP Zone", "Standard Area", "Bar Area"
using MusicLounge.Domain.Common;

namespace MusicLounge.Domain.Entities;

public class SeatingZone : BaseEntity<int>
{
    public int LoungeId { get; set; }
    public string Name { get; set; } = string.Empty;
    // Maximum physical capacity of this zone — can be overridden per show via TicketPrice.Quota
    public int Capacity { get; set; }
    public string? Description { get; set; }
    public int DisplayOrder { get; set; } = 0;
    public bool IsActive { get; set; } = true;
}
