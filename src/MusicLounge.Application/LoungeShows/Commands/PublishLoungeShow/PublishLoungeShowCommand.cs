using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.LoungeShows.Commands.PublishLoungeShow;

public sealed record PublishLoungeShowCommand(int ShowId) : ICommand;
