using FluentValidation;

namespace MusicLounge.Application.Users.Commands.ChangePassword;

public sealed class ChangePasswordCommandValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordCommandValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty().WithMessage("Vui lòng nhập mật khẩu hiện tại.");

        // Kept in sync with RegisterCommandValidator / ResetPasswordCommandValidator — same
        // NIST SP 800-63B-4 / OWASP basis (no MFA on this system => 15-char single-factor minimum).
        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("Mật khẩu mới không được để trống.")
            .MinimumLength(15).WithMessage("Mật khẩu mới phải có ít nhất 15 ký tự.")
            .MaximumLength(64).WithMessage("Mật khẩu mới không được vượt quá 64 ký tự.");

        RuleFor(x => x.NewPassword)
            .NotEqual(x => x.CurrentPassword)
            .WithMessage("Mật khẩu mới phải khác mật khẩu hiện tại.");
    }
}
