using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Moderations.Commands.ReviewLivestream;

public sealed record ReviewLivestreamCommand(
    int LivestreamId,
    string Decision,
    string? ReviewNote
) : ICommand;
