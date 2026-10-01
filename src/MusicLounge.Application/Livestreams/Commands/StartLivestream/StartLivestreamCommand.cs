using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Livestreams.Commands.StartLivestream;

public sealed record StartLivestreamCommand(Guid LivestreamId) : ICommand;
