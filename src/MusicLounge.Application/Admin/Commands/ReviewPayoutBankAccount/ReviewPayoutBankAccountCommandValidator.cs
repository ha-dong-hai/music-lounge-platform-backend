using FluentValidation;

namespace MusicLounge.Application.Admin.Commands.ReviewPayoutBankAccount;

public sealed class ReviewPayoutBankAccountCommandValidator : AbstractValidator<ReviewPayoutBankAccountCommand>
{
    public ReviewPayoutBankAccountCommandValidator()
    {
        RuleFor(x => x.BankAccountId).GreaterThan(0);

        RuleFor(x => x.Note)
            .NotEmpty()
            .When(x => !x.Approve)
            .WithMessage("Từ chối tài khoản nhận tiền phải nêu lý do — chủ phòng trà cần biết phải sửa gì.");

        RuleFor(x => x.Note).MaximumLength(1000);
    }
}
