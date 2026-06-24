using MusicLounge.Domain.Common;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Domain.Entities;

public class Ticket : BaseEntity<Guid>
{
    // Nullable — SET NULL when buyer account is deleted (BVDLCN 2025)
    public int? BuyerId { get; set; }
    public int TicketPriceId { get; set; }
    public AccessType Type { get; set; }
    public TicketStatus Status { get; set; } = TicketStatus.Pending;
    public DateTime PurchasedAt { get; set; }
}
