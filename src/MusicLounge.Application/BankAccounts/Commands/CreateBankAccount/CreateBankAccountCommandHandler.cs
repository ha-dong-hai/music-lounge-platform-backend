using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;

namespace MusicLounge.Application.BankAccounts.Commands.CreateBankAccount;

internal sealed class CreateBankAccountCommandHandler : IRequestHandler<CreateBankAccountCommand, int>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public CreateBankAccountCommandHandler(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
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
        }

        var account = new BankAccount
        {
            OwnerType = request.OwnerType,
            OwnerId = request.OwnerId,
            BankName = request.BankName,
            AccountNumber = request.AccountNumber,
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
