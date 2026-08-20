using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Lounges.Commands.UpdateLounge;

public sealed record UpdateLoungeCommand(
    int LoungeId,
    string Name,
    string? Description,
    int? AtmosphereId,
    string Street,
    string Ward,
    string District,
    string City,
    double? Latitude,
    double? Longitude
) : ICommand;
