using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.LoungeShows.Commands.AddPerformance;

public sealed record AddPerformanceCommand(
    Guid ShowId,
    Guid? PerformerId,
    string? PerformerName,
    string Role,
    int OrderIndex,
    TimeOnly? SetTime,
    bool AcceptsDonation
) : ICommand<Guid>;
