using FluentValidation;

namespace MusicLounge.Application.BankAccounts.Commands.CreateBankAccount;

public sealed class CreateBankAccountCommandValidator : AbstractValidator<CreateBankAccountCommand>
{
    public CreateBankAccountCommandValidator()
    {
        RuleFor(x => x.OwnerId).GreaterThan(0);
        RuleFor(x => x.BankName).NotEmpty().MaximumLength(255);
        RuleFor(x => x.AccountNumber).NotEmpty().MaximumLength(50);
        RuleFor(x => x.AccountHolder).NotEmpty().MaximumLength(255);
    }
}
