using System;

namespace MusicLounge.Domain.Entities;

public class EventAreaTicket
{
    public int Id { get; set; }
    public int EventId { get; set; }
    public int AreaId { get; set; }
    public decimal Price { get; set; }
    public int TotalQuota { get; set; }
}

