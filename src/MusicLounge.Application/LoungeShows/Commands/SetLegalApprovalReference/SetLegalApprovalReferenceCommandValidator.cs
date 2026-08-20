using FluentValidation;

namespace MusicLounge.Application.LoungeShows.Commands.SetLegalApprovalReference;

public sealed class SetLegalApprovalReferenceCommandValidator : AbstractValidator<SetLegalApprovalReferenceCommand>
{
    public SetLegalApprovalReferenceCommandValidator()
    {
        RuleFor(x => x.ShowId).GreaterThan(0);
        RuleFor(x => x.LegalApprovalReference).NotEmpty().MaximumLength(500);
    }
}
