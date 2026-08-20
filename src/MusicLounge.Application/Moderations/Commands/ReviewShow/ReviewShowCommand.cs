using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Moderations.Commands.ReviewShow;

public sealed record ReviewShowCommand(
    int ShowId,
    string Decision,
    string? ReviewNote
) : ICommand;
