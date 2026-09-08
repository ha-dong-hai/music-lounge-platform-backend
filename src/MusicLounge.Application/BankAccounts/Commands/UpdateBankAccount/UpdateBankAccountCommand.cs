using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.BankAccounts.Commands.UpdateBankAccount;

public sealed record UpdateBankAccountCommand(
    int Id,
    string BankName,
    string AccountNumber,
    string AccountHolder,
    bool IsDefault) : ICommand;
