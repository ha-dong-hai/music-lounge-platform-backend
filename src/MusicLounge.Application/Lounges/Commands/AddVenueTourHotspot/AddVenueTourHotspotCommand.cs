using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Lounges.Commands.AddVenueTourHotspot;

public sealed record AddVenueTourHotspotCommand(
    Guid LoungeId,
    Guid SceneId,
    string Type,
    double Yaw,
    double Pitch,
    string? Label,
    Guid? TargetSceneId,
    string? InfoText
) : ICommand<Guid>;
