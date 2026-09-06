using FluentValidation;

namespace MusicLounge.Application.Auth.Commands.ResetPassword;

public sealed class ResetPasswordCommandValidator : AbstractValidator<ResetPasswordCommand>
{
    public ResetPasswordCommandValidator()
    {
        RuleFor(x => x.Token).NotEmpty().WithMessage("Thiếu token đặt lại mật khẩu.");

        // Khớp ngưỡng với RegisterCommandValidator — xem comment ở đó.
        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("Mật khẩu không được để trống.")
            .MinimumLength(10).WithMessage("Mật khẩu phải có ít nhất 10 ký tự.");
    }
}
