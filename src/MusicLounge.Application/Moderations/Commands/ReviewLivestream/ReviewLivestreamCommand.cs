using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Moderations.Commands.ReviewLivestream;

public sealed record ReviewLivestreamCommand(
    Guid LivestreamId,
    string Decision,
    string? ReviewNote
) : ICommand;
