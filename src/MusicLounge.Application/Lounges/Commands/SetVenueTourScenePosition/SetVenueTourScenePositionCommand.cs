using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Lounges.Commands.SetVenueTourScenePosition;

public sealed record SetVenueTourScenePositionCommand(
    Guid LoungeId,
    Guid SceneId,
    double? X,
    double? Y
) : ICommand;
