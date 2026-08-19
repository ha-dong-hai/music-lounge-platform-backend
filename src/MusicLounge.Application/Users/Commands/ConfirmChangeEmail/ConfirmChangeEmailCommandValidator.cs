using FluentValidation;

namespace MusicLounge.Application.Users.Commands.ConfirmChangeEmail;

public sealed class ConfirmChangeEmailCommandValidator : AbstractValidator<ConfirmChangeEmailCommand>
{
    public ConfirmChangeEmailCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().WithMessage("Vui lòng nhập mã xác thực.");
    }
}
