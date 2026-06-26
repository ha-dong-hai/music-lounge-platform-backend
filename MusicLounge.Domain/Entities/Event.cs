using System;

namespace MusicLounge.Domain.Entities;

public class Event
{
    public int Id { get; set; }
    public int LoungeId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateOnly EventDate { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public string? PosterUrl { get; set; }
    public int OnlineQuota { get; set; }
    public int OfflineQuota { get; set; }
    public DateTime? TicketSaleClosesAt { get; set; }
    public bool CancellationAllowed { get; set; }
    public int? CancellationDeadlineHours { get; set; }
    public decimal? RefundPercentage { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

