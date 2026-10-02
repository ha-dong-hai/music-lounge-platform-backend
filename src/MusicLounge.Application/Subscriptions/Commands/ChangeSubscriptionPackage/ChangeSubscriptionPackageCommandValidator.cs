using FluentValidation;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;

namespace MusicLounge.Application.Subscriptions.Commands.ChangeSubscriptionPackage;

public sealed class ChangeSubscriptionPackageCommandValidator : AbstractValidator<ChangeSubscriptionPackageCommand>
{
    public ChangeSubscriptionPackageCommandValidator(IUnitOfWork uow)
    {
        RuleFor(x => x.PackageId)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .MustAsync(async (packageId, ct) =>
                await uow.Repository<SubscriptionPackage, Guid>().AnyAsync(p => p.Id == packageId, ct))
            .WithMessage("PackageId không tồn tại.");
    }
}
