using System;

namespace MusicLounge.Domain.Entities;

public class SeatingArea
{
    public int Id { get; set; }
    public int LoungeId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public string? Description { get; set; }
}

