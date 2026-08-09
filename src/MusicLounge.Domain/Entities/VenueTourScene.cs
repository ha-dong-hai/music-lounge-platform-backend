namespace MusicLounge.Domain.Entities;

// One 360° panorama photo of the venue (Owner-captured, e.g. with a phone 360 camera app) — a
// stop in the walkthrough, analogous to one "room" in a Louvre/HCMC-Museum-style virtual tour.
// Deliberately separate from Model3DUrl on MusicLounge (a single .glb/.gltf 3D model file for a
// generic code-built scene) — different content pipeline entirely, not a replacement for it.
public sealed class VenueTourScene : Common.BaseEntity<int>
{
    public int LoungeId { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public string? Name { get; set; }
    public int OrderIndex { get; set; }

    // Marker position on the venue's floor-plan overview (MusicLounge.AreaLayoutImageUrl — reused
    // as-is, same image SeatingZone.Layout2D already draws its zone rectangles on). % coordinates
    // (0-100), independent of screen resolution — same convention as SeatingZone.Layout2DX/Y.
    // Null = not placed yet; the scene list alone still works without this.
    public double? PositionX { get; set; }
    public double? PositionY { get; set; }

    public MusicLounge Lounge { get; set; } = null!;
    public ICollection<VenueTourHotspot> Hotspots { get; set; } = [];
}
