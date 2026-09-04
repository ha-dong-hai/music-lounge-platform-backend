using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Lounges.Commands.SetVenueTourScenePosition;

public sealed record SetVenueTourScenePositionCommand(
    int LoungeId,
    int SceneId,
    double? X,
    double? Y
) : ICommand;
