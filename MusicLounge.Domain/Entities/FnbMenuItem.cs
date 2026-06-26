using System;

namespace MusicLounge.Domain.Entities;

public class FnbMenuItem
{
    public int Id { get; set; }
    public int LoungeId { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public bool IsAvailable { get; set; }
    public DateTime CreatedAt { get; set; }
}

