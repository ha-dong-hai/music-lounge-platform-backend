using System;

namespace MusicLounge.Domain.Entities;

public class MusicGenre
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

