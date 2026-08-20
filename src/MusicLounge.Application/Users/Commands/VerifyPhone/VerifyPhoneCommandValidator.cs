using FluentValidation;

namespace MusicLounge.Application.Users.Commands.VerifyPhone;

public sealed class VerifyPhoneCommandValidator : AbstractValidator<VerifyPhoneCommand>
{
    public VerifyPhoneCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().Length(6);
    }
}
