namespace MusicLounge.Domain.Entities;

public sealed class LoungeShowMood : Common.BaseEntity<int>
{
    public int LoungeShowId { get; set; }
    public int MoodId { get; set; }

    public LoungeShow LoungeShow { get; set; } = null!;
    public Mood Mood { get; set; } = null!;
}
