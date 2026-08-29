using FluentValidation;

namespace MusicLounge.Application.LoungeShows.Commands.RemoveRating;

public sealed class RemoveRatingCommandValidator : AbstractValidator<RemoveRatingCommand>
{
    public RemoveRatingCommandValidator()
    {
        RuleFor(x => x.RatingId).GreaterThan(0).WithMessage("RatingId không hợp lệ.");
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Phải nêu lý do gỡ đánh giá.")
            .MaximumLength(500).WithMessage("Lý do không được vượt quá 500 ký tự.");
    }
}
