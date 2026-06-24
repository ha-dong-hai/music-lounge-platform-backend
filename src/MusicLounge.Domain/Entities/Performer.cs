using MusicLounge.Domain.Common;

namespace MusicLounge.Domain.Entities;

public class Performer : BaseEntity<int>
{
    // solo or band
    public string Type { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Bio { get; set; }
    public string? PhotoUrl { get; set; }
    // Nullable — SET NULL when creator account is deleted (BVDLCN 2025)
    public int? CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
}
