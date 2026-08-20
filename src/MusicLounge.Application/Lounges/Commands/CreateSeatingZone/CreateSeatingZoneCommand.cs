using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Lounges.Commands.CreateSeatingZone;

public sealed record CreateSeatingZoneCommand(
    int LoungeId,
    string Name,
    string? Description,
    int Capacity
) : ICommand<int>;
