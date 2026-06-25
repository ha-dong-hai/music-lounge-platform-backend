// CoreFlow: CF3 (Ticket Booking — F&B ordering during show)
// Food and beverage items on a venue's menu.
using MusicLounge.Domain.Common;

namespace MusicLounge.Domain.Entities;

public class FnbMenuItem : BaseEntity<int>
{
    public int LoungeId { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsAvailable { get; set; } = true;
    public int DisplayOrder { get; set; } = 0;
    public DateTime CreatedAt { get; set; }
}
