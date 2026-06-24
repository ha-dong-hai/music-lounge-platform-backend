using MusicLounge.Domain.Common;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Domain.Entities;

public class Performer : BaseEntity<int>
{
    public PerformerType Type { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? Bio { get; set; }
    public string? PhotoUrl { get; set; }
    // Nullable — SET NULL when creator account is deleted (BVDLCN 2025)
    public int? CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
}
