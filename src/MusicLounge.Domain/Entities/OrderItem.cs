namespace MusicLounge.Domain.Entities;

public sealed class OrderItem : Common.BaseEntity<int>
{
    public int FnbOrderId { get; set; }
    public int MenuItemId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }  // D12: snapshot at order time — never recomputed
    public bool Cancelled { get; set; } = false;
    public string? Note { get; set; }
    // subtotal NOT stored — computed: Quantity × UnitPrice

    public FnbOrder Order { get; set; } = null!;
    public FnbMenuItem MenuItem { get; set; } = null!;
}
