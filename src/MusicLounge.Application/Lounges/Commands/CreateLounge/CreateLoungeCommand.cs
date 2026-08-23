using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Lounges.Commands.CreateLounge;

public sealed record CreateLoungeCommand(
    string Name,
    string? Description,
    int? AtmosphereId,
    string Street,
    string Ward,
    string District,
    string City,
    double? Latitude,
    double? Longitude
) : ICommand<int>;
