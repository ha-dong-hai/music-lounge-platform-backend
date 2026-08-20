namespace MusicLounge.Domain.Enums;

public enum VenueTourHotspotType
{
    // Clicking it jumps the viewer to TargetSceneId — the "walk to the next room" arrow.
    Navigate,
    // Clicking it shows InfoText in place — a static caption/description, no navigation.
    Info
}
