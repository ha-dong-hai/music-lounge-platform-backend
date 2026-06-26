using System;

namespace MusicLounge.Domain.Entities;

public class Ticket
{
    public Guid Id { get; set; }
    public int EventId { get; set; }
    public int BuyerId { get; set; }
    public int? AreaId { get; set; }
    public string TicketType { get; set; } = string.Empty;
    public string QrCode { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime PurchasedAt { get; set; }
    public DateTime? CheckedInAt { get; set; }
    public DateTime? OnlineVerifiedAt { get; set; }
}

