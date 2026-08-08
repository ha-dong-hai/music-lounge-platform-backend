using Microsoft.EntityFrameworkCore;
using MusicLounge.Application.Common.Interfaces.Repositories;
using MusicLounge.Domain.Entities;
using MusicLounge.Infrastructure.Persistence;

namespace MusicLounge.Infrastructure.Repositories;

internal sealed class LedgerEntryRepository : Repository<LedgerEntry, int>, ILedgerEntryRepository
{
    private readonly ApplicationDbContext _ctx;

    public LedgerEntryRepository(ApplicationDbContext ctx) : base(ctx) => _ctx = ctx;

    public async Task<IReadOnlyList<JournalBalance>> GetImbalancedJournalsAsync(CancellationToken ct = default)
    {
        // GroupBy followed by 2 differently-filtered Sum()s in the same Select does not translate
        // under the SQLite provider used in tests (same class of "conditional aggregate in one
        // GroupBy" limitation documented throughout this codebase) — narrow the server round-trip
        // to just the 3 columns actually needed instead of full LedgerEntry rows, then group/sum
        // client-side. Still far less data pulled than the old GetAllAsync() (whole entity, every
        // column, every row).
        var rows = await _ctx.LedgerEntries
            .AsNoTracking()
            .Select(e => new { e.JournalId, e.Amount, e.IsDebit })
            .ToListAsync(ct);

        return rows
            .GroupBy(e => e.JournalId)
            .Select(g => new JournalBalance(
                g.Key,
                g.Where(e => e.IsDebit).Sum(e => e.Amount),
                g.Where(e => !e.IsDebit).Sum(e => e.Amount)))
            .Where(x => x.DebitTotal != x.CreditTotal)
            .ToList();
    }
}
