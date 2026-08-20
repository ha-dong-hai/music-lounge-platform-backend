using FluentValidation;

namespace MusicLounge.Application.Lounges.Commands.SetLoungeBusinessLicense;

public sealed class SetLoungeBusinessLicenseCommandValidator : AbstractValidator<SetLoungeBusinessLicenseCommand>
{
    public SetLoungeBusinessLicenseCommandValidator()
    {
        RuleFor(x => x.LoungeId).GreaterThan(0);
        RuleFor(x => x.DocumentUrl).NotEmpty().MaximumLength(500);
    }
}
