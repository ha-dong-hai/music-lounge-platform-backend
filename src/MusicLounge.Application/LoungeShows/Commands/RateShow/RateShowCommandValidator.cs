using FluentValidation;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;

namespace MusicLounge.Application.LoungeShows.Commands.RateShow;

internal sealed class RateShowCommandValidator : AbstractValidator<RateShowCommand>
{
    public RateShowCommandValidator(IUnitOfWork uow)
    {
        RuleFor(x => x.ShowId)
            .GreaterThan(0).WithMessage("ShowId không hợp lệ.")
            .MustAsync(async (showId, ct) => await uow.Repository<LoungeShow, int>().AnyAsync(s => s.Id == showId, ct))
            .WithMessage("ShowId không tồn tại.");
        RuleFor(x => x.Score)
            .InclusiveBetween(1, 5).WithMessage("Điểm đánh giá phải từ 1 đến 5.");
        RuleFor(x => x.Comment)
            .MaximumLength(1000).WithMessage("Bình luận không vượt quá 1000 ký tự.")
            .When(x => x.Comment is not null);
    }
}
