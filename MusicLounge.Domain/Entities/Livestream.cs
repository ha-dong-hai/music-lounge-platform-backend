using System;

namespace MusicLounge.Domain.Entities;

public class Livestream
{
    public int Id { get; set; }
    public int EventId { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public string? StreamUrl { get; set; }
    public string? RecordingUrl { get; set; }
    public DateTime? RewatchUntil { get; set; }
    public string Status { get; set; } = string.Empty;
}

