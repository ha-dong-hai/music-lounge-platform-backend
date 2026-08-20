using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Lounges.Commands.SetZoneLayout2D;

public sealed record SetZoneLayout2DCommand(
    int ZoneId,
    double X,
    double Y,
    double Width,
    double Height,
    double RotationDeg,
    string? Color) : ICommand;
