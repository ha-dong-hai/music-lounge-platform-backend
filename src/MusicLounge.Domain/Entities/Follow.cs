namespace MusicLounge.Domain.Entities;

public sealed class Follow : Common.BaseEntity<Guid>
{
    public Guid UserId { get; set; }
    public Guid LoungeId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public User User { get; set; } = null!;
    public MusicLounge Lounge { get; set; } = null!;
}
