using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.Exceptions;

namespace MusicLounge.Application.Common.Services;

// Scoped: the account cache lives for one request so repeated journals
// in the same unit of work reuse not-yet-saved Account instances.
internal sealed class LedgerService : ILedgerService
{
    private readonly IUnitOfWork _uow;
    private readonly Dictionary<(AccountType, int?), Account> _accountCache = [];

    public LedgerService(IUnitOfWork uow) => _uow = uow;

    public async Task WriteJournalAsync(
        string journalId,
        string referenceType,
        string referenceId,
        int? paymentId,
        IReadOnlyList<LedgerLine> lines,
        CancellationToken ct = default)
    {
        var debit = lines.Where(l => l.IsDebit).Sum(l => l.Amount);
        var credit = lines.Where(l => !l.IsDebit).Sum(l => l.Amount);
        if (debit != credit)
            throw new DomainException(
                $"Journal {journalId} không cân: tổng debit ({debit}) phải bằng tổng credit ({credit}).");

        var entryRepo = _uow.Repository<LedgerEntry, int>();
        var now = DateTimeOffset.UtcNow;

        foreach (var line in lines)
        {
            var account = await GetOrCreateAccountAsync(line.OwnerType, line.OwnerId, ct);
            entryRepo.Add(new LedgerEntry
            {
                JournalId = journalId,
                Account = account,
                Amount = line.Amount,
                IsDebit = line.IsDebit,
                ReferenceType = referenceType,
                ReferenceId = referenceId,
                PaymentId = paymentId,
                Description = line.Description,
                CreatedAt = now
            });
        }
    }

    private async Task<Account> GetOrCreateAccountAsync(AccountType ownerType, int? ownerId, CancellationToken ct)
    {
        if (_accountCache.TryGetValue((ownerType, ownerId), out var cached))
            return cached;

        var repo = _uow.Repository<Account, int>();
        var account = (await repo.FindAsync(a => a.OwnerType == ownerType && a.OwnerId == ownerId, ct))
            .FirstOrDefault();

        if (account is null)
        {
            account = new Account { OwnerType = ownerType, OwnerId = ownerId };
            repo.Add(account);
        }
        else
        {
            // FindAsync reads via AsNoTracking, so this row isn't tracked yet in this unit of
            // work. Left as-is, assigning it to a new LedgerEntry's navigation and Add()-ing
            // that entry would cascade this already-existing Account into an INSERT too —
            // re-attach it so EF treats it as an existing row instead.
            repo.Update(account);
        }

        _accountCache[(ownerType, ownerId)] = account;
        return account;
    }
}
