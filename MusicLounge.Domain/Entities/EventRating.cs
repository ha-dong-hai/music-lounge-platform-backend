using System;

namespace MusicLounge.Domain.Entities;

public class EventRating
{
    public int Id { get; set; }
    public int EventId { get; set; }
    public int UserId { get; set; }
    public int Stars { get; set; }
    public string? ReviewText { get; set; }
    public bool IsRemoved { get; set; }
    public DateTime CreatedAt { get; set; }
}

