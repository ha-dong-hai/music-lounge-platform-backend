using FluentValidation;

namespace MusicLounge.Application.Auth.Commands.VerifyEmail;

public sealed class VerifyEmailCommandValidator : AbstractValidator<VerifyEmailCommand>
{
    public VerifyEmailCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email không được để trống.")
            .EmailAddress().WithMessage("Email không hợp lệ.")
            .MaximumLength(255);

        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Mã xác thực không được để trống.")
            .Matches(@"^\d{6}$").WithMessage("Mã xác thực phải gồm đúng 6 chữ số.");
    }
}
