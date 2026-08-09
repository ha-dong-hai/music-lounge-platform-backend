using FluentValidation;

namespace MusicLounge.Application.BankAccounts.Commands.UpdateBankAccount;

public sealed class UpdateBankAccountCommandValidator : AbstractValidator<UpdateBankAccountCommand>
{
    public UpdateBankAccountCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.BankName).NotEmpty().MaximumLength(255);
        RuleFor(x => x.AccountNumber).NotEmpty().MaximumLength(50);
        RuleFor(x => x.AccountHolder).NotEmpty().MaximumLength(255);
    }
}
