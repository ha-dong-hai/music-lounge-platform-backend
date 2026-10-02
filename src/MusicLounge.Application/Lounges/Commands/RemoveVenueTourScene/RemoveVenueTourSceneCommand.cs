using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Lounges.Commands.RemoveVenueTourScene;

public sealed record RemoveVenueTourSceneCommand(Guid LoungeId, Guid SceneId) : ICommand;
