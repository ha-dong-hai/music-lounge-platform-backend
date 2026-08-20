using FluentValidation;

namespace MusicLounge.Application.Auth.Commands.GoogleLogin;

public sealed class GoogleLoginCommandValidator : AbstractValidator<GoogleLoginCommand>
{
    public GoogleLoginCommandValidator()
    {
        RuleFor(x => x.IdToken).NotEmpty().WithMessage("IdToken không được để trống.");
    }
}
