using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.LoungeShows.Commands.RemoveFromWishlist;

public sealed record RemoveFromWishlistCommand(Guid ShowId) : ICommand;
