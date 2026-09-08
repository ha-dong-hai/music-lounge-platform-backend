using MusicLounge.Application.BankAccounts.DTOs;
using MusicLounge.Application.Common.Abstractions;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.BankAccounts.Queries.GetBankAccounts;

public sealed record GetBankAccountsQuery(
    BankAccountOwnerType OwnerType, int OwnerId) : IQuery<IReadOnlyList<BankAccountDto>>;
