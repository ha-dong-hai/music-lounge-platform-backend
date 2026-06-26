using System;

namespace MusicLounge.Domain.Entities;

public class AiGeneratedPoster
{
    public int Id { get; set; }
    public int EventId { get; set; }
    public int OwnerId { get; set; }
    public string? Prompt { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsSelected { get; set; }
    public DateTime CreatedAt { get; set; }
}

