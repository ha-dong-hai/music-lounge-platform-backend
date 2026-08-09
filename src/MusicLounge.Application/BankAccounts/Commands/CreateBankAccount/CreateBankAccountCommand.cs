using MediatR;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.BankAccounts.Commands.CreateBankAccount;

public sealed record CreateBankAccountCommand(
    BankAccountOwnerType OwnerType,
    int OwnerId,
    string BankName,
    string AccountNumber,
    string AccountHolder,
    bool IsDefault) : IRequest<int>;
