using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Follows.Commands.FollowLounge;

public sealed record FollowLoungeCommand(int LoungeId) : ICommand;
