// CoreFlow: CF3 (Ticket Booking)
// Additional details for physical (in-person) tickets only — 1-to-1 with Ticket.
using MusicLounge.Domain.Common;

namespace MusicLounge.Domain.Entities;

public class PhysicalTicketDetail : BaseEntity<int>
{
    public Guid TicketId { get; set; }
    // Separate UUID used for QR code — can be regenerated if compromised without changing TicketId
    public string QrCode { get; set; } = string.Empty;
    // Null = purchased online; set when Staff sells ticket at venue counter
    public int? SoldByStaffId { get; set; }
    public int? CheckedInById { get; set; }
    public DateTime? CheckedInAt { get; set; }
    // Free-text seat note e.g. "Table A3"
    public string? SeatNote { get; set; }
}
