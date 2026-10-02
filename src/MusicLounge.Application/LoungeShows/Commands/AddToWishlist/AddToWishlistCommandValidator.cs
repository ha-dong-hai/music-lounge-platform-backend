using FluentValidation;

namespace MusicLounge.Application.LoungeShows.Commands.AddToWishlist;

public sealed class AddToWishlistCommandValidator : AbstractValidator<AddToWishlistCommand>
{
    public AddToWishlistCommandValidator()
    {
        RuleFor(x => x.ShowId)
            .NotEmpty().WithMessage("ShowId không hợp lệ.");
    }
}
