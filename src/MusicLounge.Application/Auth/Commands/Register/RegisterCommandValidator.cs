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

        // 15 ký tự — NIST SP 800-63B rev 4 (pages.nist.gov/800-63-4/sp800-63b.html) quy định
        // verifier SHALL yêu cầu tối thiểu 15 ký tự khi mật khẩu được dùng làm yếu tố xác thực DUY
        // NHẤT; mốc 8 ký tự chỉ áp dụng cho mật khẩu nằm trong một quy trình đa yếu tố. MusicLounge
        // hiện không có MFA, nên thuộc nhóm 15. Ngưỡng 10 trước đây được đặt dựa trên giả định
        // "trên mức sàn 8" — giả định đó sai, vì mức sàn 8 không áp dụng cho hệ thống không MFA.
        // Trần 64 ký tự theo khuyến nghị SHOULD ở cùng mục, để không chặn passphrase dài.
        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Mật khẩu không được để trống.")
            .MinimumLength(15).WithMessage(
                "Mật khẩu phải có ít nhất 15 ký tự. Mẹo: một cụm từ dễ nhớ thường vừa dài vừa an toàn hơn " +
                "một chuỗi ký tự rối, ví dụ \"toi thich nghe nhac trinh\".")
            .MaximumLength(64).WithMessage("Mật khẩu không được dài quá 64 ký tự.");

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
