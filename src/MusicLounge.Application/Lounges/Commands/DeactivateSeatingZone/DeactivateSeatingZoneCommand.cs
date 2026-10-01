using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Lounges.Commands.DeactivateSeatingZone;

public sealed record DeactivateSeatingZoneCommand(Guid ZoneId) : ICommand;
