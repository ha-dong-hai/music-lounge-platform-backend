namespace MusicLounge.Domain.Entities;

public sealed class Follow : Common.BaseEntity<int>
{
    public int UserId { get; set; }
    public int LoungeId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public User User { get; set; } = null!;
    public MusicLounge Lounge { get; set; } = null!;
}
