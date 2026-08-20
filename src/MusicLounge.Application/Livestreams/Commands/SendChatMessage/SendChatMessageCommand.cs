using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Livestreams.Commands.SendChatMessage;

public sealed record SendChatMessageCommand(
    int LivestreamId,
    int UserId,
    string Message) : ICommand<int>;
