using FluentValidation;

namespace MusicLounge.Application.LoungeShows.Commands.RemoveFromWishlist;

public sealed class RemoveFromWishlistCommandValidator : AbstractValidator<RemoveFromWishlistCommand>
{
    public RemoveFromWishlistCommandValidator()
    {
        RuleFor(x => x.ShowId).GreaterThan(0).WithMessage("ShowId không hợp lệ.");
    }
}
