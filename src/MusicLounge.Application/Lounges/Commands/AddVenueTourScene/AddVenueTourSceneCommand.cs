using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Lounges.Commands.AddVenueTourScene;

public sealed record AddVenueTourSceneCommand(
    int LoungeId,
    string ImageUrl,
    string? Name
) : ICommand<int>;
