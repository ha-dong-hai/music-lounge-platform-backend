using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.LoungeShows.Commands.UpdatePerformance;

public sealed record UpdatePerformanceCommand(
    Guid PerformanceId,
    string Role,
    int OrderIndex,
    TimeOnly? SetTime,
    bool AcceptsDonation
) : ICommand;
