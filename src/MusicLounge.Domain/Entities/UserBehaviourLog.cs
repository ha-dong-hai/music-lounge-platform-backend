using MusicLounge.Domain.Enums;

namespace MusicLounge.Domain.Entities;

public sealed class UserBehaviourLog : Common.BaseEntity<int>
{
    public int UserId { get; set; }
    public int LoungeShowId { get; set; }
    public BehaviourAction Action { get; set; }
    public int? DurationSeconds { get; set; }
    public string? Metadata { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public User User { get; set; } = null!;
    public LoungeShow LoungeShow { get; set; } = null!;
}
