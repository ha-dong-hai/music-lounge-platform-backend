using System;

namespace MusicLounge.Domain.Entities;

public class EventModeration
{
    public int Id { get; set; }
    public int EventId { get; set; }
    public decimal? AiScore { get; set; }
    public string? RiskLevel { get; set; }
    public string? FlagReason { get; set; }
    public string? AiDecision { get; set; }
    public int? AdminId { get; set; }
    public string? AdminDecision { get; set; }
    public string? ReviewNote { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
}

