using MusicLounge.Domain.Common;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Domain.Entities;

public class TicketPrice : BaseEntity<int>
{
    public int ShowId { get; set; }
    // Null = no seating zone (online/livestream tickets)
    public int? ZoneId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public int Quota { get; set; }
    // Atomic counter — incremented only when payment is confirmed, never decremented directly
    public int Sold { get; set; } = 0;
    public PurchaseChannel PurchaseChannel { get; set; }
    public AccessType AccessType { get; set; }
    public DateTime? SaleStart { get; set; }
    public DateTime? SaleEnd { get; set; }
    public bool IsActive { get; set; } = true;
}
