using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Livestreams.Commands.TerminateLivestream;

public sealed record TerminateLivestreamCommand(
    int LivestreamId,
    string Reason
) : ICommand;
