using FluentValidation;

namespace MusicLounge.Application.Users.Commands.RequestChangeEmail;

public sealed class RequestChangeEmailCommandValidator : AbstractValidator<RequestChangeEmailCommand>
{
    public RequestChangeEmailCommandValidator()
    {
        RuleFor(x => x.NewEmail)
            .NotEmpty().WithMessage("Email không được để trống.")
            .EmailAddress().WithMessage("Email không hợp lệ.")
            .MaximumLength(255);
    }
}
