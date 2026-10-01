using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.LoungeShows.Commands.StartLoungeShow;

public sealed record StartLoungeShowCommand(Guid ShowId) : ICommand;
