// CoreFlow: CF3 (Ticket Booking — F&B ordering during show)
// A line item within an F&B order.
// UnitPrice is a snapshot taken at order time — menu price changes do not affect existing orders (D12).
using MusicLounge.Domain.Common;

namespace MusicLounge.Domain.Entities;

public class OrderItem : BaseEntity<int>
{
    public int FnbOrderId { get; set; }
    public int MenuItemId { get; set; }
    public int Quantity { get; set; }
    // Snapshot of menu item price at order time
    public decimal UnitPrice { get; set; }
    public bool Cancelled { get; set; } = false;
    public string? Note { get; set; }
}
