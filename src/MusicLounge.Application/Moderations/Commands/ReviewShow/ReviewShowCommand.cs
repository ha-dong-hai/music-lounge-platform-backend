using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Moderations.Commands.ReviewShow;

public sealed record ReviewShowCommand(
    Guid ShowId,
    string Decision,
    string? ReviewNote
) : ICommand;
