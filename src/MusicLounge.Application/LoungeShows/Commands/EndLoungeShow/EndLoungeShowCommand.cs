using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.LoungeShows.Commands.EndLoungeShow;

public sealed record EndLoungeShowCommand(int ShowId) : ICommand;
