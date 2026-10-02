using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.LoungeShows.Commands.SetShowPoster;

public sealed record SetShowPosterCommand(Guid ShowId, string ImageUrl) : ICommand;
