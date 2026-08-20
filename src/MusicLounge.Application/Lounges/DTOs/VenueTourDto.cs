namespace MusicLounge.Application.Lounges.DTOs;

public sealed record VenueTourHotspotDto(
    int Id,
    string Type,
    double Yaw,
    double Pitch,
    string? Label,
    int? TargetSceneId,
    string? InfoText);

public sealed record VenueTourSceneDto(
    int Id,
    string ImageUrl,
    string? Name,
    int OrderIndex,
    double? PositionX,
    double? PositionY,
    IReadOnlyList<VenueTourHotspotDto> Hotspots);

public sealed record VenueTourDto(
    int LoungeId,
    // Same floor-plan image SeatingZone.Layout2D already draws zone rectangles on — reused as the
    // background for each scene's PositionX/PositionY marker, rather than a second image field.
    string? FloorPlanImageUrl,
    IReadOnlyList<VenueTourSceneDto> Scenes);

// Polled by the Owner after StitchTourScene returns an attempt id (stitching runs in the
// background - see StitchVenueTourSceneCommandHandler). ResultSceneId is only set once Status is
// "Succeeded"; ErrorMessage only once Status is "Failed".
public sealed record VenueTourStitchAttemptDto(
    int Id,
    string Status,
    int? ResultSceneId,
    string? ErrorMessage);
