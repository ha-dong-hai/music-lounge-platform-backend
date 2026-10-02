using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.LoungeShows.Commands.RateShow;

public sealed record RateShowCommand(Guid ShowId, int Score, string? Comment) : ICommand;
