namespace MusicLounge.Domain.Entities;

public sealed class ShowWishlist : Common.BaseEntity<Guid>
{
    public Guid UserId { get; set; }
    public Guid LoungeShowId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public User User { get; set; } = null!;
    public LoungeShow LoungeShow { get; set; } = null!;
}
