using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.LoungeShows.Commands.CancelLoungeShow;

public sealed record CancelLoungeShowCommand(int ShowId) : ICommand;
