using FluentValidation;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;

namespace MusicLounge.Application.Subscriptions.Commands.SubscribeToPackage;

public sealed class SubscribeToPackageCommandValidator : AbstractValidator<SubscribeToPackageCommand>
{
    public SubscribeToPackageCommandValidator(IUnitOfWork uow)
    {
        RuleFor(x => x.PackageId)
            .GreaterThan(0)
            .MustAsync(async (packageId, ct) =>
                await uow.Repository<SubscriptionPackage, int>().AnyAsync(p => p.Id == packageId, ct))
            .WithMessage("PackageId không tồn tại.");
    }
}
