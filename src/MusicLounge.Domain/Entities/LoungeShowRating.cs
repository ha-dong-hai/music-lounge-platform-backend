namespace MusicLounge.Domain.Entities;

public sealed class LoungeShowRating : Common.BaseEntity<int>
{
    public int? UserId { get; set; }
    public int LoungeShowId { get; set; }
    public int Score { get; set; }
    public string? Comment { get; set; }
    public bool IsRemoved { get; set; } = false;
    public string? RemovedReason { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public User? User { get; set; }
    public LoungeShow LoungeShow { get; set; } = null!;
}
