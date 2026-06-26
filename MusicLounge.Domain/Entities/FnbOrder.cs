using System;

namespace MusicLounge.Domain.Entities;

public class FnbOrder
{
    public int Id { get; set; }
    public int EventId { get; set; }
    public int AudienceId { get; set; }
    public int? AreaId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? PaymentMethod { get; set; }
    public bool PaymentConfirmed { get; set; }
    public decimal TotalAmount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

