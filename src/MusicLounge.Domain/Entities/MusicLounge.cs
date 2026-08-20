using MusicLounge.Domain.Enums;
using MusicLounge.Domain.ValueObjects;

namespace MusicLounge.Domain.Entities;

public sealed class MusicLounge : Common.AuditableEntity<int>
{
    public int OwnerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? PrimaryImageUrl { get; set; }
    public string? AreaLayoutImageUrl { get; set; }
    public string? BusinessLicenseUrl { get; set; }
    // Tour ao 3D: file .glb/.gltf that Owner upload cho khong gian phong tra. Null = dung scene
    // mau dung code (khong can noi dung 3D that de co san chuc nang tu ngay dau).
    public string? Model3DUrl { get; set; }
    public int? AtmosphereId { get; set; }
    public LoungeStatus Status { get; set; } = LoungeStatus.Pending;
    public decimal ReputationScore { get; set; } = 0m;  // D3: star-rating scale 0–5, thresholds at 3.5 / 4.2

    // Owned Value Object — map thành cột phẳng trong bảng Lounges
    public VenueAddress Address { get; set; } = new();

    public User Owner { get; set; } = null!;
    public VenueAtmosphere? Atmosphere { get; set; }
    public ICollection<LoungeShow> LoungeShows { get; set; } = [];
    public ICollection<Follow> Follows { get; set; } = [];
    public ICollection<FnbMenu> Menus { get; set; } = [];
}
