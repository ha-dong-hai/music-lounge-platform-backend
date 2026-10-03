namespace MusicLounge.Domain.Enums;

public enum VenueTourHotspotType
{
    // Clicking it jumps the viewer to TargetSceneId — the "walk to the next room" arrow.
    Navigate,
    // Clicking it shows InfoText in place — a static caption/description, no navigation.
    Info,
    // MLACP-555: points at one SeatingZone (ZoneId) seen in the panorama — on a show's ticket tab the
    // Audience taps it to pick that zone, the same way they tap a zone on the 2D/3D seating map.
    // Selling stays per ZONE (no numbered seats): owner chose "chọn khu" over seat/table selection
    // 03/10/2026; seat-level selling would need seat holds + migration of the ticket flow.
    Zone
}
