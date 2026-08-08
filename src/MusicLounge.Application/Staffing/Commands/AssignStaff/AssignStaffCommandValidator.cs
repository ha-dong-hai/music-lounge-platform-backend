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
            .GreaterThan(0)
            .MustAsync(async (loungeId, ct) =>
                await uow.Repository<MusicLoungeEntity, int>().AnyAsync(l => l.Id == loungeId, ct))
            .WithMessage("LoungeId không tồn tại.");

        RuleFor(x => x.UserId)
            .GreaterThan(0)
            .MustAsync(async (userId, ct) => await uow.Repository<User, int>().AnyAsync(u => u.Id == userId, ct))
            .WithMessage("UserId không tồn tại.");
    }
}
