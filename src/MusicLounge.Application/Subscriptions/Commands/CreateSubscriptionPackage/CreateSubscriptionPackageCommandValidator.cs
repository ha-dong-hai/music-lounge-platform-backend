using FluentValidation;

namespace MusicLounge.Application.Subscriptions.Commands.CreateSubscriptionPackage;

public sealed class CreateSubscriptionPackageCommandValidator : AbstractValidator<CreateSubscriptionPackageCommand>
{
    private static readonly string[] ValidCycles = ["Monthly", "Quarterly", "Yearly"];

    public CreateSubscriptionPackageCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.Price).GreaterThan(0);
        RuleFor(x => x.BillingCycle)
            .Must(c => ValidCycles.Contains(c, StringComparer.OrdinalIgnoreCase))
            .WithMessage("BillingCycle phải là 'Monthly', 'Quarterly' hoặc 'Yearly'.");
        RuleFor(x => x.MaxTicketsPerEvent).GreaterThan(0);
    }
}
