namespace MusicLounge.Domain.Entities;

public sealed class FnbMenuItem : Common.BaseEntity<int>
{
    public int MenuId { get; set; }
    public string Category { get; set; } = string.Empty;   // Food / Drink / etc.
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsAvailable { get; set; } = true;
    public int DisplayOrder { get; set; } = 0;
    public DateTimeOffset CreatedAt { get; set; }

    public FnbMenu Menu { get; set; } = null!;
    public ICollection<OrderItem> OrderItems { get; set; } = [];
}
