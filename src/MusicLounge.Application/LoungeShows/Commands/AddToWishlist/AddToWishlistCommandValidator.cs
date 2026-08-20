using FluentValidation;

namespace MusicLounge.Application.LoungeShows.Commands.AddToWishlist;

public sealed class AddToWishlistCommandValidator : AbstractValidator<AddToWishlistCommand>
{
    public AddToWishlistCommandValidator()
    {
        RuleFor(x => x.ShowId)
            .GreaterThan(0).WithMessage("ShowId không hợp lệ.");
    }
}
