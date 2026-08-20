using MediatR;
using MusicLounge.Application.BankAccounts.DTOs;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;

namespace MusicLounge.Application.BankAccounts.Queries.GetBankAccounts;

internal sealed class GetBankAccountsQueryHandler
    : IRequestHandler<GetBankAccountsQuery, IReadOnlyList<BankAccountDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly IPiiEncryptionService _piiEncryption;

    public GetBankAccountsQueryHandler(
        IUnitOfWork uow, ICurrentUserService currentUser, IPiiEncryptionService piiEncryption)
    {
        _uow = uow;
        _currentUser = currentUser;
        _piiEncryption = piiEncryption;
    }

    public async Task<IReadOnlyList<BankAccountDto>> Handle(GetBankAccountsQuery request, CancellationToken ct)
    {
        await BankAccountAccess.EnsureCanManageAsync(
            _uow, _currentUser, request.OwnerType, request.OwnerId, ct);

        var accounts = await _uow.Repository<BankAccount, int>().FindAsync(
            a => a.OwnerType == request.OwnerType && a.OwnerId == request.OwnerId, ct);

        // Decrypt only here, at the one boundary already gated by BankAccountAccess — every other
        // handler that touches BankAccount (settlement/donation payout wiring) only needs .Id, never
        // the plaintext number, and should stay working on ciphertext.
        return accounts
            .OrderByDescending(a => a.IsDefault)
            .ThenBy(a => a.Id)
            .Select(a => new BankAccountDto(
                a.Id, a.OwnerType, a.OwnerId, a.BankName, _piiEncryption.Decrypt(a.AccountNumber), a.AccountHolder,
                a.IsDefault, a.IsVerified))
            .ToList();
    }
}
