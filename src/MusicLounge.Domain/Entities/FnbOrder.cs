using MusicLounge.Domain.Enums;

namespace MusicLounge.Domain.Entities;

public sealed class FnbOrder : Common.BaseEntity<int>
{
    public int LoungeId { get; set; }
    public int? ShowId { get; set; }            // null = outside show hours
    public int? AudienceUserId { get; set; }    // BVDLCN: SET NULL on delete
    public int? StaffId { get; set; }           // staff who placed on behalf of guest
    public int? ZoneId { get; set; }
    public string? TableNote { get; set; }      // "Bàn A3"
    public FnbOrderStatus Status { get; set; } = FnbOrderStatus.Pending;
    public PaymentMethod PaymentMethod { get; set; }
    public decimal TotalAmount { get; set; } = 0m;
    public string? Note { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public MusicLounge Lounge { get; set; } = null!;
    public LoungeShow? Show { get; set; }
    public User? AudienceUser { get; set; }
    public User? Staff { get; set; }
    public SeatingZone? Zone { get; set; }
    public ICollection<OrderItem> Items { get; set; } = [];
}
