using System;

namespace MusicLounge.Domain.Entities;

public class FnbOrderItem
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public int MenuItemId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public bool Cancelled { get; set; }
}

