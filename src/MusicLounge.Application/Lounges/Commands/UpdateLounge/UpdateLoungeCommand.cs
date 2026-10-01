using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Lounges.Commands.UpdateLounge;

public sealed record UpdateLoungeCommand(
    Guid LoungeId,
    string Name,
    string? Description,
    Guid? AtmosphereId,
    string Street,
    string Ward,
    string? District,
    string City,
    double? Latitude,
    double? Longitude
) : ICommand;
