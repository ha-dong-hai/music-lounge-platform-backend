namespace MusicLounge.Domain.Entities;

public sealed class TicketHold : Common.BaseEntity<int>
{
    public int UserId { get; set; }
    public int PriceId { get; set; }
    public int Quantity { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public bool IsReleased { get; set; } = false;
    public DateTimeOffset? ReleasedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public User User { get; set; } = null!;
    public TicketPrice Price { get; set; } = null!;
}
