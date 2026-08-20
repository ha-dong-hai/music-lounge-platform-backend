using MusicLounge.Domain.Enums;

namespace MusicLounge.Domain.Entities;

public sealed class TicketTier : Common.AuditableEntity<int>
{
    public int LoungeShowId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public AccessType AccessType { get; set; }
    public int? TotalCapacity { get; set; }
    public int? ZoneId { get; set; }   // D1: null = online (no physical zone)

    public LoungeShow LoungeShow { get; set; } = null!;
    public SeatingZone? Zone { get; set; }

    public ICollection<TicketPrice> Prices { get; set; } = [];
}
