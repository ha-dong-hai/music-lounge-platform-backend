using MusicLounge.Domain.Common;

namespace MusicLounge.Domain.Entities;

public class MusicLounge : AuditableEntity<int>
{
    public int OwnerId { get; set; }
    // Catalog item managed by Admin — affects AI atmosphere matching (CF2)
    public int? AtmosphereId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Address { get; set; } = string.Empty;
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public string? AreaLayoutImageUrl { get; set; }
    // Required for Admin approval — venue must upload before going live
    public string? BusinessLicenseUrl { get; set; }
    // Determines settlement tier: <3.5 = New (50%), 3.5–4.2 = Standard (70%), ≥4.2 = Premium (80%)
    public decimal ReputationScore { get; set; } = 0;
    // pending / approved / warned / suspended / locked — controlled by Admin only
    public string Status { get; set; } = "pending";
}
