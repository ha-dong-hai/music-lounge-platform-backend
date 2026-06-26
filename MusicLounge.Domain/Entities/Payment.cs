using System;

namespace MusicLounge.Domain.Entities;

public class Payment
{
    public int Id { get; set; }
    public Guid? TicketId { get; set; }
    public int? SubscriptionId { get; set; }
    public int PayerId { get; set; }
    public decimal Amount { get; set; }
    public string PaymentType { get; set; } = string.Empty;
    public string? Destination { get; set; }
    public string? GatewayRef { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

