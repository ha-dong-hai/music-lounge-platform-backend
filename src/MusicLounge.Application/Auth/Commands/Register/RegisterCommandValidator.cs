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

        // 15 chars, no composition rules — NIST SP 800-63B-4 §5.1.1.2 (single-factor memorized
        // secret, since login here has no MFA) + OWASP Authentication Cheat Sheet: below 15 chars
        // is "weak" without MFA; forced uppercase/digit/symbol rules are explicitly NOT recommended
        // (they push users toward predictable patterns rather than real entropy).
        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Mật khẩu không được để trống.")
            .MinimumLength(15).WithMessage("Mật khẩu phải có ít nhất 15 ký tự.")
            .MaximumLength(64).WithMessage("Mật khẩu không được vượt quá 64 ký tự.");

        // Unicode letter/mark categories + space/apostrophe/hyphen/period — OWASP Input Validation
        // Cheat Sheet's "name field" guidance (allowlist character categories, keep apostrophe for
        // names like "O'Brian", don't require ASCII-only so "Nguyễn Văn A" passes). Scoped to this
        // field only: other free-text Name fields in this codebase (venue/bank/product) are legitimately
        // punctuation/digit-heavy ("T&T Lounge", "5 Seconds of Summer") and must stay unrestricted —
        // this is a real person's legal name, not a display label.
        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Họ tên không được để trống.")
            .MaximumLength(255)
            .Matches(@"^[\p{L}\p{M} .'\-]+$").WithMessage("Họ tên chỉ được chứa chữ cái và các ký tự . ' -.");

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
