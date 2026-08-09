using MusicLounge.Domain.Enums;

namespace MusicLounge.Domain.Entities;

// A clickable point placed inside one panorama scene, at spherical position (Yaw, Pitch) — the
// same coordinate concept every 360 viewer library uses (krpano, Marzipano, Pannellum, Photo
// Sphere Viewer), so this stays renderable regardless of which one the frontend picks.
public sealed class VenueTourHotspot : Common.BaseEntity<int>
{
    public int SceneId { get; set; }
    // Only set (and only meaningful) when Type == Navigate — the scene this hotspot "walks" to.
    public int? TargetSceneId { get; set; }
    public VenueTourHotspotType Type { get; set; }
    public double Yaw { get; set; }     // horizontal angle, -180..180
    public double Pitch { get; set; }   // vertical angle, -90..90
    public string? Label { get; set; }
    public string? InfoText { get; set; }

    public VenueTourScene Scene { get; set; } = null!;
    public VenueTourScene? TargetScene { get; set; }
}
