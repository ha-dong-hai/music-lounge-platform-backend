using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Lounges.Commands.UpdateSeatingZone;

public sealed record UpdateSeatingZoneCommand(
    Guid ZoneId,
    string Name,
    string? Description,
    int Capacity
) : ICommand;
