using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Lounges.Commands.RemoveVenueTourScene;

public sealed record RemoveVenueTourSceneCommand(int LoungeId, int SceneId) : ICommand;
