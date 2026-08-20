namespace MusicLounge.Domain.Entities;

public sealed class ShowWishlist : Common.BaseEntity<int>
{
    public int UserId { get; set; }
    public int LoungeShowId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public User User { get; set; } = null!;
    public LoungeShow LoungeShow { get; set; } = null!;
}
