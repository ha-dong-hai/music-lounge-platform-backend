using FluentValidation;

namespace MusicLounge.Application.Auth.Commands.Register;

public sealed class RegisterCommandValidator : AbstractValidator<RegisterCommand>
{
    public RegisterCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email không được để trống.")
            .EmailAddress().WithMessage("Email không hợp lệ.")
            .MaximumLength(255);

        // 10 ký tự — NIST SP 800-63B ưu tiên độ dài hơn độ phức tạp bắt buộc, nhưng tài khoản trên
        // nền tảng này (đặc biệt Owner, có quyền truy cập BankAccount/hủy show) đáng được đặt cao
        // hơn mức sàn 8 ký tự. [CHƯA KIỂM CHỨNG: chưa tra cứu được con số khuyến nghị chính xác của
        // bản NIST SP 800-63B mới nhất — cần xác minh lại nếu muốn trích dẫn con số cụ thể.]
        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Mật khẩu không được để trống.")
            .MinimumLength(10).WithMessage("Mật khẩu phải có ít nhất 10 ký tự.");

        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Họ tên không được để trống.")
            .MaximumLength(255);

        RuleFor(x => x.Phone)
            .MaximumLength(20)
            .When(x => x.Phone is not null);

        RuleFor(x => x.Role)
            .Must(r => r is "Audience" or "Owner")
            .WithMessage("Role tự đăng ký chỉ có thể là 'Audience' hoặc 'Owner'.");

        // Luật 91/2025/QH15 lawful-basis requirement — must be an explicit, affirmative choice from
        // the client (a checkbox the user actually ticks), never a pre-checked/defaulted-true value.
        RuleFor(x => x.AcceptTerms)
            .Equal(true).WithMessage("Bạn cần đồng ý với Điều khoản dịch vụ và Chính sách bảo mật để đăng ký.");
    }
}
