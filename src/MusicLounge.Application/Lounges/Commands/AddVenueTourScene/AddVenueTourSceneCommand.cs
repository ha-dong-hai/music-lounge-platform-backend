using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Lounges.Commands.AddVenueTourScene;

public sealed record AddVenueTourSceneCommand(
    Guid LoungeId,
    string ImageUrl,
    string? Name
) : ICommand<Guid>;
