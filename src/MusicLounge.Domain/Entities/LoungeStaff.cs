namespace MusicLounge.Domain.Entities;

// D6: Staff are scoped to a specific venue — JWT role alone is not enough
public sealed class LoungeStaff : Common.BaseEntity<int>
{
    public int LoungeId { get; set; }
    public int UserId { get; set; }
    public int AssignedBy { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset AssignedAt { get; set; }
    public DateTimeOffset? DeactivatedAt { get; set; }

    public MusicLounge Lounge { get; set; } = null!;
    public User User { get; set; } = null!;
    public User AssignedByUser { get; set; } = null!;
}
