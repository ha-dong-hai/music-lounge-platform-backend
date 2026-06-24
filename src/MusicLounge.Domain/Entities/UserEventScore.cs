using MusicLounge.Domain.Common;

namespace MusicLounge.Domain.Entities;

public class UserEventScore : BaseEntity<int>
{
    public int UserId { get; set; }
    public int ShowId { get; set; }
    // Composite score computed from attended, rated, donated, wishlisted, viewed signals
    public decimal Score { get; set; } = 0;
    // JSON breakdown: { attended, rating, donated, wishlist, view }
    public string? Breakdown { get; set; }
    public DateTime ComputedAt { get; set; }
}
