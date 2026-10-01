using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.LoungeShows.Commands.AddToWishlist;

public sealed record AddToWishlistCommand(Guid ShowId) : ICommand;
