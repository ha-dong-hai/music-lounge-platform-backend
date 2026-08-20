namespace MusicLounge.Domain.Entities;

public sealed class AiRecommendation : Common.BaseEntity<int>
{
    public int UserId { get; set; }
    public int LoungeShowId { get; set; }
    public float FinalScore { get; set; }
    public float ContentScore { get; set; }
    public float CollabScore { get; set; }
    public float CustomScore { get; set; }
    public string Algorithm { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }

    public User User { get; set; } = null!;
    public LoungeShow LoungeShow { get; set; } = null!;
}
