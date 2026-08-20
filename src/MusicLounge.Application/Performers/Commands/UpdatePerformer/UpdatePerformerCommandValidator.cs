using FluentValidation;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.Performers.Commands.UpdatePerformer;

internal sealed class UpdatePerformerCommandValidator : AbstractValidator<UpdatePerformerCommand>
{
    public UpdatePerformerCommandValidator(IUnitOfWork uow)
    {
        RuleFor(x => x.PerformerId).GreaterThan(0);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.AvatarUrl).MaximumLength(500);
        RuleFor(x => x.Bio).MaximumLength(2000);

        RuleFor(x => x.Type)
            .Must(t => Enum.TryParse<PerformerType>(t, ignoreCase: true, out _))
            .WithMessage($"Type phải là một trong: {string.Join(", ", Enum.GetNames<PerformerType>())}.");

        RuleForEach(x => x.GenreIds)
            .MustAsync(async (id, ct) => await uow.Repository<MusicGenre, int>().AnyAsync(g => g.Id == id, ct))
            .WithMessage("GenreId không tồn tại.");
    }
}
