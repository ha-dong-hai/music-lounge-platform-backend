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
    // MLACP-391: ai da go — doi xung voi AssignedBy. Truoc day chi con trong log, khong co dau vet ben de doi soat khi
    // Admin can thiep (MLACP-381). Null voi cac dong go truoc khi co cot nay.
    public int? DeactivatedBy { get; set; }

    public MusicLounge Lounge { get; set; } = null!;
    public User User { get; set; } = null!;
    public User AssignedByUser { get; set; } = null!;
    public User? DeactivatedByUser { get; set; }
}
