using FluentValidation;

namespace MusicLounge.Application.Subscriptions.Commands.SubscribeToPackage;

public sealed class SubscribeToPackageCommandValidator : AbstractValidator<SubscribeToPackageCommand>
{
    public SubscribeToPackageCommandValidator()
    {
        RuleFor(x => x.PackageId).GreaterThan(0);
    }
}
