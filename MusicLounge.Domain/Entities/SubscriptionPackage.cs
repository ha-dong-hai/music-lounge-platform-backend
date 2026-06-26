using System;

namespace MusicLounge.Domain.Entities;

public class SubscriptionPackage
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int DurationDays { get; set; }
    public int MaxTicketsPerEvent { get; set; }
    public bool HasAiPoster { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

