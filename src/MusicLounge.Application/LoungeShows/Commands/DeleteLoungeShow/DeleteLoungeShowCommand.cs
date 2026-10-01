using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.LoungeShows.Commands.DeleteLoungeShow;

public sealed record DeleteLoungeShowCommand(Guid ShowId) : ICommand;
