using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.BankAccounts.Commands.UpdateBankAccount;

public sealed record UpdateBankAccountCommand(
    Guid Id,
    string BankName,
    string AccountNumber,
    string AccountHolder,
    bool IsDefault) : ICommand;
