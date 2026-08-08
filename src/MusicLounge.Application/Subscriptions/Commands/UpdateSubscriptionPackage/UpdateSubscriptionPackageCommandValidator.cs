using FluentValidation;

namespace MusicLounge.Application.Subscriptions.Commands.UpdateSubscriptionPackage;

public sealed class UpdateSubscriptionPackageCommandValidator : AbstractValidator<UpdateSubscriptionPackageCommand>
{
    public UpdateSubscriptionPackageCommandValidator()
    {
        RuleFor(x => x.PackageId).GreaterThan(0);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.Price).GreaterThan(0);
        RuleFor(x => x.MaxTicketsPerEvent).GreaterThan(0);
    }
}
