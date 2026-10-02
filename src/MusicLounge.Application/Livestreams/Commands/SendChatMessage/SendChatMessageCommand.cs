using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Livestreams.Commands.SendChatMessage;

public sealed record SendChatMessageCommand(
    Guid LivestreamId,
    Guid UserId,
    string Message) : ICommand<Guid>;
