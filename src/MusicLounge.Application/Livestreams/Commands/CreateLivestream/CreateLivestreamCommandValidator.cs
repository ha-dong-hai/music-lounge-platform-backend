using FluentValidation;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;

namespace MusicLounge.Application.Livestreams.Commands.CreateLivestream;

public sealed class CreateLivestreamCommandValidator : AbstractValidator<CreateLivestreamCommand>
{
    public CreateLivestreamCommandValidator(IUnitOfWork uow)
    {
        RuleFor(x => x.ShowId)
            .Cascade(CascadeMode.Stop)
            .GreaterThan(0)
            .MustAsync(async (showId, ct) => await uow.Repository<LoungeShow, int>().AnyAsync(s => s.Id == showId, ct))
            .WithMessage("ShowId không tồn tại.");
    }
}
