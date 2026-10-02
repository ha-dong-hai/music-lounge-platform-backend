using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Livestreams.Commands.EndLivestream;

public sealed record EndLivestreamCommand(Guid LivestreamId) : ICommand;
