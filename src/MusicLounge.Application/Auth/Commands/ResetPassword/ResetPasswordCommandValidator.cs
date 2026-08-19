using FluentValidation;

namespace MusicLounge.Application.Auth.Commands.ResetPassword;

public sealed class ResetPasswordCommandValidator : AbstractValidator<ResetPasswordCommand>
{
    public ResetPasswordCommandValidator()
    {
        RuleFor(x => x.Token).NotEmpty().WithMessage("Thiếu token đặt lại mật khẩu.");

        // Kept in sync with RegisterCommandValidator — same NIST SP 800-63B-4 / OWASP basis.
        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("Mật khẩu không được để trống.")
            .MinimumLength(15).WithMessage("Mật khẩu phải có ít nhất 15 ký tự.")
            .MaximumLength(64).WithMessage("Mật khẩu không được vượt quá 64 ký tự.");
    }
}
