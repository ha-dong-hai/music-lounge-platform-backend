using FluentValidation;

namespace MusicLounge.Application.Donations.Commands.AcknowledgeDonation;

public sealed class AcknowledgeDonationCommandValidator
    : AbstractValidator<AcknowledgeDonationCommand>
{
    public AcknowledgeDonationCommandValidator()
    {
        RuleFor(x => x.DonationId)
            .GreaterThan(0)
            .WithMessage("DonationId không hợp lệ.");
    }
}
