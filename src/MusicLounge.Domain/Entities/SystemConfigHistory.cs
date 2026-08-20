namespace MusicLounge.Domain.Entities;

// D9: IMMUTABLE audit trail — INSERT only, never UPDATE or DELETE
public sealed class SystemConfigHistory : Common.BaseEntity<long>
{
    public string ConfigKey { get; set; } = string.Empty;
    public string? OldValue { get; set; }
    public string NewValue { get; set; } = string.Empty;
    public DateTimeOffset EffectiveFrom { get; set; }   // must be >= NOW()
    public int ChangedBy { get; set; }
    public DateTimeOffset ChangedAt { get; set; }
    public string Note { get; set; } = string.Empty;    // mandatory — reason for change

    public SystemConfig Config { get; set; } = null!;
    public User ChangedByUser { get; set; } = null!;
}
