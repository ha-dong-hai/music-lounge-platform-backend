using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.LoungeShows.Commands.SetShowCoverImage;

public sealed record SetShowCoverImageCommand(int ShowId, string ImageUrl) : ICommand;
