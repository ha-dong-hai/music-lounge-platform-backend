using System;

namespace MusicLounge.Domain.Entities;

public class TicketHold
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int EventAreaTicketId { get; set; }
    public int Quantity { get; set; }
    public DateTime HeldUntil { get; set; }
    public bool Released { get; set; }
    public DateTime CreatedAt { get; set; }
}

