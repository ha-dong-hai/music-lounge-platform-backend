namespace MusicLounge.Application.Lounges.DTOs;

public sealed record SeatingZoneDto(
    int Id,
    int LoungeId,
    string Name,
    string? Description,
    int Capacity,
    int DisplayOrder,
    bool IsActive,
    string? LayoutColor,
    double? Layout2DX,
    double? Layout2DY,
    double? Layout2DWidth,
    double? Layout2DHeight,
    double? Layout2DRotationDeg,
    double? Layout3DX,
    double? Layout3DY,
    double? Layout3DZ);
