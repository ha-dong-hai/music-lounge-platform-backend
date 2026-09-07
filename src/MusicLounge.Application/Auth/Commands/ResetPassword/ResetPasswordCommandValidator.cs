using FluentValidation;

namespace MusicLounge.Application.Auth.Commands.ResetPassword;

public sealed class ResetPasswordCommandValidator : AbstractValidator<ResetPasswordCommand>
{
    public ResetPasswordCommandValidator()
    {
        RuleFor(x => x.Token).NotEmpty().WithMessage("Thiếu token đặt lại mật khẩu.");

        // Khớp ngưỡng với RegisterCommandValidator — xem comment ở đó (NIST SP 800-63B rev 4).
        // Hai chỗ phải luôn bằng nhau: nếu đặt lại mật khẩu dễ hơn đăng ký, thì ngưỡng đăng ký chỉ
        // còn là hình thức — ai cũng đi vòng qua được bằng một lượt "Quên mật khẩu".
        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("Mật khẩu không được để trống.")
            .MinimumLength(15).WithMessage(
                "Mật khẩu phải có ít nhất 15 ký tự. Mẹo: một cụm từ dễ nhớ thường vừa dài vừa an toàn hơn " +
                "một chuỗi ký tự rối, ví dụ \"toi thich nghe nhac trinh\".")
            .MaximumLength(64).WithMessage("Mật khẩu không được dài quá 64 ký tự.");
    }
}
