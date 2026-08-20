using MusicLounge.Domain.Enums;

namespace MusicLounge.Domain.Entities;

public sealed class Performance : Common.BaseEntity<int>
{
    public int LoungeShowId { get; set; }
    public int PerformerId { get; set; }
    public PerformerRole Role { get; set; } = PerformerRole.Main;
    public int OrderIndex { get; set; }
    public TimeOnly? SetTime { get; set; }
    public bool AcceptsDonation { get; set; } = true;

    public LoungeShow LoungeShow { get; set; } = null!;
    public Performer Performer { get; set; } = null!;
}
