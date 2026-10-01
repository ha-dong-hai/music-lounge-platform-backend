namespace MusicLounge.Domain.Entities;

public sealed class LoungeShowMood : Common.BaseEntity<Guid>
{
    public Guid LoungeShowId { get; set; }
    public Guid MoodId { get; set; }

    public LoungeShow LoungeShow { get; set; } = null!;
    public Mood Mood { get; set; } = null!;
}
