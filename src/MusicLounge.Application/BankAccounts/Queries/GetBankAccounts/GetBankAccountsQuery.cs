using MediatR;
using MusicLounge.Application.BankAccounts.DTOs;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.BankAccounts.Queries.GetBankAccounts;

public sealed record GetBankAccountsQuery(
    BankAccountOwnerType OwnerType, int OwnerId) : IRequest<IReadOnlyList<BankAccountDto>>;
