namespace MusicLounge.Domain.Common;

public abstract class AuditableEntity<TKey> : BaseEntity<TKey>
{
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
