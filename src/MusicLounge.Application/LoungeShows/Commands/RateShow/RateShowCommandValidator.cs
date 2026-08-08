using FluentValidation;

namespace MusicLounge.Application.LoungeShows.Commands.RateShow;

internal sealed class RateShowCommandValidator : AbstractValidator<RateShowCommand>
{
    public RateShowCommandValidator()
    {
        RuleFor(x => x.ShowId).GreaterThan(0).WithMessage("ShowId không hợp lệ.");
        RuleFor(x => x.Score)
            .InclusiveBetween(1, 5).WithMessage("Điểm đánh giá phải từ 1 đến 5.");
        RuleFor(x => x.Comment)
            .MaximumLength(1000).WithMessage("Bình luận không vượt quá 1000 ký tự.")
            .When(x => x.Comment is not null);
    }
}
