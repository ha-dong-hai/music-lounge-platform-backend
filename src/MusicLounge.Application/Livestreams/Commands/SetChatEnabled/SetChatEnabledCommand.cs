using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Livestreams.Commands.SetChatEnabled;

public sealed record SetChatEnabledCommand(int LivestreamId, bool Enabled) : ICommand;
