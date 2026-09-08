using FluentValidation;

namespace MusicLounge.Application.Notifications.Commands.UnregisterDeviceToken;

public sealed class UnregisterDeviceTokenCommandValidator : AbstractValidator<UnregisterDeviceTokenCommand>
{
    public UnregisterDeviceTokenCommandValidator()
    {
        RuleFor(x => x.Token).NotEmpty().WithMessage("Token không được rỗng.");
    }
}
