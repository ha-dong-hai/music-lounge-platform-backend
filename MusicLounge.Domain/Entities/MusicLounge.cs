using System;

namespace MusicLounge.Domain.Entities;

public class MusicLounge
{
    public int Id { get; set; }
    public int OwnerId { get; set; }
    public int? AtmosphereId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public int CapacityTotal { get; set; }
    public string? AreaLayoutImageUrl { get; set; }
    public string? Description { get; set; }
    public string? BankAccountNumber { get; set; }
    public string? BankName { get; set; }
    public bool IsEscrowRequired { get; set; }
    public int ReputationScore { get; set; }
    public DateTime CreatedAt { get; set; }
}

