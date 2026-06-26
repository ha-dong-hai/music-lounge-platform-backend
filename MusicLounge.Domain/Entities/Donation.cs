using System;

namespace MusicLounge.Domain.Entities;

public class Donation
{
    public int Id { get; set; }
    public int LivestreamId { get; set; }
    public int DonorId { get; set; }
    public int ArtistId { get; set; }
    public int PaymentId { get; set; }
    public decimal Amount { get; set; }
    public string? DisplayName { get; set; }
    public string? Message { get; set; }
    public DateTime CreatedAt { get; set; }
}

