using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Follows.Commands.UnfollowLounge;

public sealed record UnfollowLoungeCommand(int LoungeId) : ICommand;
