using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.LoungeShows.Commands.AddPerformance;

public sealed record AddPerformanceCommand(
    int ShowId,
    int? PerformerId,
    string? PerformerName,
    string Role,
    int OrderIndex,
    TimeOnly? SetTime,
    bool AcceptsDonation
) : ICommand<int>;
