using FluentValidation;

namespace MusicLounge.Application.Donations.Queries.GetOwnerDonationHistory;

public sealed class GetOwnerDonationHistoryQueryValidator : AbstractValidator<GetOwnerDonationHistoryQuery>
{
    public GetOwnerDonationHistoryQueryValidator()
    {
        RuleFor(x => x)
            .Must(x => !x.From.HasValue || !x.To.HasValue || x.From <= x.To)
            .WithMessage("'From' phải trước hoặc bằng 'To'.");
    }
}
