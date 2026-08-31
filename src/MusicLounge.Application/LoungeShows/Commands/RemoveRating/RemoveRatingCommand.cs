using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.LoungeShows.Commands.RemoveRating;

public sealed record RemoveRatingCommand(int RatingId, string Reason) : ICommand;
