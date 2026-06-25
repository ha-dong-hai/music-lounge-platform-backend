// CoreFlow: CF3 (Ticket Booking)
// Temporarily reserves ticket quota for a user during checkout.
// Prevents oversell when multiple users attempt to buy the same tickets simultaneously.
// Hold duration is read from system_config.ticket_hold_minutes (default 15 min).
using MusicLounge.Domain.Common;

namespace MusicLounge.Domain.Entities;

public class TicketHold : BaseEntity<int>
{
    public int UserId { get; set; }
    public int TicketPriceId { get; set; }
    public int Quantity { get; set; }
    public DateTime HeldUntil { get; set; }
    // False = hold is active; True = released by payment or expired by Hangfire job
    public bool IsReleased { get; set; } = false;
    public DateTime CreatedAt { get; set; }
}
