using MusicLounge.Domain.Enums;

namespace MusicLounge.Domain.Entities;

public sealed class FnbOrder : Common.AuditableEntity<Guid>
{
    public Guid LoungeId { get; set; }
    public Guid? ShowId { get; set; }            // null = outside show hours
    public Guid? AudienceUserId { get; set; }    // BVDLCN: SET NULL on delete
    public Guid? StaffId { get; set; }           // staff who placed on behalf of guest
    public Guid? ZoneId { get; set; }
    public string? TableNote { get; set; }      // "Bàn A3"
    public FnbOrderStatus Status { get; set; } = FnbOrderStatus.Pending;
    public PaymentMethod PaymentMethod { get; set; }
    public decimal TotalAmount { get; set; } = 0m;
    public string? Note { get; set; }

    public MusicLounge Lounge { get; set; } = null!;
    public LoungeShow? Show { get; set; }
    public User? AudienceUser { get; set; }
    public User? Staff { get; set; }
    public SeatingZone? Zone { get; set; }
    public ICollection<OrderItem> Items { get; set; } = [];
}
