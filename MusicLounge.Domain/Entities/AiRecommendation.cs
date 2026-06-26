using System;

namespace MusicLounge.Domain.Entities;

public class AiRecommendation
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int EventId { get; set; }
    public decimal RecommendationScore { get; set; }
    public string? Reason { get; set; }
    public DateTime CreatedAt { get; set; }
}

