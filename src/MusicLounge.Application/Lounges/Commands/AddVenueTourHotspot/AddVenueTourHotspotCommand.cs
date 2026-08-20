using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Lounges.Commands.AddVenueTourHotspot;

public sealed record AddVenueTourHotspotCommand(
    int LoungeId,
    int SceneId,
    string Type,
    double Yaw,
    double Pitch,
    string? Label,
    int? TargetSceneId,
    string? InfoText
) : ICommand<int>;
