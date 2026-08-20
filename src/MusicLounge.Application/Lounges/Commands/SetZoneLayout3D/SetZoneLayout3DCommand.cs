using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Lounges.Commands.SetZoneLayout3D;

public sealed record SetZoneLayout3DCommand(int ZoneId, double? X, double? Y, double? Z) : ICommand;
