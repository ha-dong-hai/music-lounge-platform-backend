using Microsoft.EntityFrameworkCore;
using MusicLounge.Application.Common.Interfaces.Repositories;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.Users.DTOs;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
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

    public async Task<PaginatedResult<OwnerTransactionDto>> GetOwnerHistoryAsync(
        int ownerId, string? referenceType, DateTimeOffset? from, DateTimeOffset? to,
        int page, int pageSize, CancellationToken ct = default)
    {
        var query = _ctx.LedgerEntries.AsNoTracking()
            .Where(e => e.Account.OwnerType == AccountType.User && e.Account.OwnerId == ownerId && !e.IsDebit);

        if (referenceType is not null)
            query = query.Where(e => e.ReferenceType == referenceType);

        // Date range filtered client-side after materializing the (already narrow, equality-only)
        // server-side result — combining an enum/int equality filter with a DateTimeOffset range
        // comparison in one Where does not reliably translate under the SQLite provider used in
        // tests, same class of limitation documented throughout this codebase's other repositories.
        var rows = await query
            .Select(e => new OwnerTransactionDto(e.Id, e.ReferenceType, e.ReferenceId, e.Amount, e.Description, e.CreatedAt))
            .ToListAsync(ct);

        var filtered = rows
            .Where(r => from is null || r.CreatedAt >= from)
            .Where(r => to is null || r.CreatedAt <= to)
            .OrderByDescending(r => r.CreatedAt)
            .ToList();

        var items = filtered.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return new PaginatedResult<OwnerTransactionDto>(items, page, pageSize, filtered.Count);
    }
}
