using FluentValidation;

namespace MusicLounge.Application.Notifications.Commands.RegisterDeviceToken;

public sealed class RegisterDeviceTokenCommandValidator : AbstractValidator<RegisterDeviceTokenCommand>
{
    public RegisterDeviceTokenCommandValidator()
    {
        RuleFor(x => x.Token)
            .NotEmpty().WithMessage("Token không được rỗng.")
            .MaximumLength(255).WithMessage("Token không được vượt quá 255 ký tự.");

        RuleFor(x => x.Platform)
            .MaximumLength(20).WithMessage("Platform không được vượt quá 20 ký tự.")
            .When(x => x.Platform is not null);
    }
}
