using FluentValidation;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Application.Staffing.Commands.AssignStaff;

public sealed class AssignStaffCommandValidator : AbstractValidator<AssignStaffCommand>
{
    public AssignStaffCommandValidator(IUnitOfWork uow)
    {
        RuleFor(x => x.LoungeId)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .MustAsync(async (loungeId, ct) =>
                await uow.Repository<MusicLoungeEntity, Guid>().AnyAsync(l => l.Id == loungeId, ct))
            .WithMessage("LoungeId không tồn tại.");

        RuleFor(x => x.UserId)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .MustAsync(async (userId, ct) => await uow.Repository<User, Guid>().AnyAsync(u => u.Id == userId, ct))
            .WithMessage("UserId không tồn tại.");
    }
}
