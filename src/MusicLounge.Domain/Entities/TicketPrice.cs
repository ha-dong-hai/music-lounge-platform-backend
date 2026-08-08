using MusicLounge.Domain.Enums;

namespace MusicLounge.Domain.Entities;

public sealed class TicketPrice : Common.BaseEntity<int>
{
    public int TierId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public int? Quota { get; set; }

    // Legacy field — never written anywhere in the codebase (audited 2026-08-05), always 0.
    // Do NOT read this for availability. The real source of truth is computed live from
    // Tickets + TicketHolds via ITicketRepository.GetReservedQuantitiesByPriceIdsAsync, which
    // both the booking handlers (write path) and the display queries (read path) now share.
    public int Sold { get; set; } = 0;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset SaleStart { get; set; }
    public DateTimeOffset SaleEnd { get; set; }
    public PurchaseChannel PurchaseChannel { get; set; }

    public TicketTier Tier { get; set; } = null!;
    public ICollection<Ticket> Tickets { get; set; } = [];
    public ICollection<TicketHold> Holds { get; set; } = [];
}
