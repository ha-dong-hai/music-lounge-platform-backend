using FluentValidation;

namespace MusicLounge.Application.LoungeShows.Commands.RemoveFromWishlist;

public sealed class RemoveFromWishlistCommandValidator : AbstractValidator<RemoveFromWishlistCommand>
{
    public RemoveFromWishlistCommandValidator()
    {
        RuleFor(x => x.ShowId).NotEmpty().WithMessage("ShowId không hợp lệ.");
    }
}
