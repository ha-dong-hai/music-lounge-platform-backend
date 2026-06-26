using System;

namespace MusicLounge.Domain.Entities;

public class Artist
{
    public int Id { get; set; }
    public int LoungeId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Bio { get; set; }
    public string? PhotoUrl { get; set; }
    public string? BankAccountNumber { get; set; }
    public string? BankName { get; set; }
    public DateTime CreatedAt { get; set; }
}

