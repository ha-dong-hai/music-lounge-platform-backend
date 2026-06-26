using System;

namespace MusicLounge.Domain.Entities;

public class OwnerSubscription
{
    public int Id { get; set; }
    public int OwnerId { get; set; }
    public int PackageId { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public string? PaymentRef { get; set; }
    public string Status { get; set; } = string.Empty;
}

