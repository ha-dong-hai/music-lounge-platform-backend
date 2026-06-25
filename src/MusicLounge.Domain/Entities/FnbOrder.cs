// CoreFlow: CF3 (Ticket Booking — F&B ordering during show)
// An F&B order placed by audience or staff during a show.
// Either AudienceUserId or StaffId must be set — both cannot be null simultaneously.
using MusicLounge.Domain.Common;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Domain.Entities;

public class FnbOrder : AuditableEntity<int>
{
    public int LoungeId { get; set; }
    // Null = order placed outside of a show (e.g. regular café service)
    public int? ShowId { get; set; }
    // Set when audience orders via mobile app
    public int? AudienceUserId { get; set; }
    // Set when staff places the order on behalf of a table
    public int? StaffId { get; set; }
    public int? ZoneId { get; set; }
    // Free-text table identifier e.g. "Table A3"
    public string? TableNote { get; set; }
    public FnbOrderStatus Status { get; set; } = FnbOrderStatus.Pending;
    public PaymentMethod PaymentMethod { get; set; }
    public decimal TotalAmount { get; set; } = 0;
    public string? Note { get; set; }
}
