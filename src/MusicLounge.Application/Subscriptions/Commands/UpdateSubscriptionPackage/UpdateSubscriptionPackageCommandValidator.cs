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
        RuleFor(x => x.MaxAiPostersPerMonth).GreaterThanOrEqualTo(0);
        RuleFor(x => x.MaxAiPostersPerMonth)
            .GreaterThan(0).WithMessage("Gói có AI poster phải cho ít nhất 1 poster/tháng.")
            .When(x => x.HasAiPoster);
        RuleFor(x => x.MaxTourScenes).GreaterThanOrEqualTo(0);
    }
}
