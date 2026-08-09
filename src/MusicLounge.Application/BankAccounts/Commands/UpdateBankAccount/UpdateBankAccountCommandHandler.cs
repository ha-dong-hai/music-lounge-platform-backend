using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Exceptions;

namespace MusicLounge.Application.BankAccounts.Commands.UpdateBankAccount;

internal sealed class UpdateBankAccountCommandHandler : IRequestHandler<UpdateBankAccountCommand, Unit>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public UpdateBankAccountCommandHandler(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
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
        }

        account.BankName = request.BankName;
        account.AccountNumber = request.AccountNumber;
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
