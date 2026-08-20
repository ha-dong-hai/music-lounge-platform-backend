using MusicLounge.Domain.Entities;

namespace MusicLounge.Application.Common.Interfaces.Repositories;

public interface ILedgerEntryRepository : IRepository<LedgerEntry, int>
{
    /// <summary>
    /// Per-journal debit/credit totals for journals where they don't match — aggregated in SQL so
    /// GetLedgerIntegrityQueryHandler doesn't have to load the entire (ever-growing, append-only)
    /// ledger_entries table into memory just to sum 2 columns grouped by JournalId.
    /// </summary>
    Task<IReadOnlyList<JournalBalance>> GetImbalancedJournalsAsync(CancellationToken ct = default);
}

public sealed record JournalBalance(string JournalId, decimal DebitTotal, decimal CreditTotal);
