using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Exceptions;

namespace MusicLounge.Application.BankAccounts.Commands.UpdateBankAccount;

internal sealed class UpdateBankAccountCommandHandler : IRequestHandler<UpdateBankAccountCommand, Unit>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly IPiiEncryptionService _piiEncryption;

    public UpdateBankAccountCommandHandler(
        IUnitOfWork uow, ICurrentUserService currentUser, IPiiEncryptionService piiEncryption)
    {
        _uow = uow;
        _currentUser = currentUser;
        _piiEncryption = piiEncryption;
    }

    public async Task<Unit> Handle(UpdateBankAccountCommand request, CancellationToken ct)
    {
        var repo = _uow.Repository<BankAccount, int>();
        var account = await repo.GetByIdAsync(request.Id, ct)
            ?? throw new NotFoundException(nameof(BankAccount), request.Id);

        await BankAccountAccess.EnsureCanManageAsync(
            _uow, _currentUser, account.OwnerType, account.OwnerId, ct);

        if (request.IsDefault && !account.IsDefault)
        {
            var others = await repo.FindAsync(
                a => a.OwnerType == account.OwnerType && a.OwnerId == account.OwnerId
                    && a.Id != account.Id && a.IsDefault, ct);
            foreach (var other in others)
            {
                other.IsDefault = false;
                repo.Update(other);
            }
            // Commit the unset in its own round-trip before this row flips to the new default — see
            // the identical comment in CreateBankAccountCommandHandler. Two sibling UPDATEs to the
            // same table have no FK-driven ordering guarantee from EF within one SaveChangesAsync;
            // the filtered unique index is checked per-statement, not deferred to commit.
            if (others.Count > 0)
                await _uow.SaveChangesAsync(ct);
        }

        account.BankName = request.BankName;
        account.AccountNumber = _piiEncryption.Encrypt(request.AccountNumber);
        account.AccountHolder = request.AccountHolder;
        account.IsDefault = request.IsDefault;
        // Any change to the account's own identifying details invalidates a prior manual
        // verification — re-verification is Admin's job, not something this command can assert.
        account.IsVerified = false;
        repo.Update(account);

        await _uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
