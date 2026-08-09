using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;

namespace MusicLounge.Application.BankAccounts.Commands.CreateBankAccount;

internal sealed class CreateBankAccountCommandHandler : IRequestHandler<CreateBankAccountCommand, int>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly IPiiEncryptionService _piiEncryption;

    public CreateBankAccountCommandHandler(
        IUnitOfWork uow, ICurrentUserService currentUser, IPiiEncryptionService piiEncryption)
    {
        _uow = uow;
        _currentUser = currentUser;
        _piiEncryption = piiEncryption;
    }

    public async Task<int> Handle(CreateBankAccountCommand request, CancellationToken ct)
    {
        await BankAccountAccess.EnsureCanManageAsync(
            _uow, _currentUser, request.OwnerType, request.OwnerId, ct);

        var repo = _uow.Repository<BankAccount, int>();

        if (request.IsDefault)
        {
            var existing = await repo.FindAsync(
                a => a.OwnerType == request.OwnerType && a.OwnerId == request.OwnerId && a.IsDefault, ct);
            foreach (var other in existing)
            {
                other.IsDefault = false;
                repo.Update(other);
            }
            // Commit the unset BEFORE inserting the new default, in its own round-trip — the filtered
            // unique index (OwnerType, OwnerId) WHERE IsDefault=1 is checked per-statement by SQL
            // Server, not deferred to transaction commit. Unsetting and inserting in a single
            // SaveChangesAsync risks EF ordering the INSERT before the UPDATE within that one
            // transaction, which would violate the index even though the end state is valid.
            if (existing.Count > 0)
                await _uow.SaveChangesAsync(ct);
        }

        var account = new BankAccount
        {
            OwnerType = request.OwnerType,
            OwnerId = request.OwnerId,
            BankName = request.BankName,
            // Encrypted at rest (IPiiEncryptionService) — same treatment as User.CitizenCardNumber,
            // for the same reason: a bank account number is sensitive financial PII, not just an
            // arbitrary string, and this codebase already has the established pattern for it.
            AccountNumber = _piiEncryption.Encrypt(request.AccountNumber),
            AccountHolder = request.AccountHolder,
            IsDefault = request.IsDefault,
            IsVerified = false,
            CreatedAt = DateTimeOffset.UtcNow
        };
        repo.Add(account);
        await _uow.SaveChangesAsync(ct);

        return account.Id;
    }
}
