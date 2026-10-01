using FluentValidation;

namespace MusicLounge.Application.LoungeShows.Commands.SetLegalApprovalReference;

public sealed class SetLegalApprovalReferenceCommandValidator : AbstractValidator<SetLegalApprovalReferenceCommand>
{
    public SetLegalApprovalReferenceCommandValidator()
    {
        RuleFor(x => x.ShowId).NotEmpty();
        RuleFor(x => x.LegalApprovalReference).NotEmpty().MaximumLength(500);
    }
}
