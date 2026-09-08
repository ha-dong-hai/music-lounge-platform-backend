using MusicLounge.Application.Common.Models;
using MusicLounge.Application.Users.DTOs;
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

    /// <summary>
    /// Owner-facing unified transaction feed (vé bán được, donate nhận, quyết toán đã nhận, và F&B
    /// khi module đó được thêm sau này) — mọi khoản Owner THỰC NHẬN đều đi qua đúng 1 điểm ghi sổ
    /// cái (D8) dưới dạng credit (!IsDebit) vào Account(User, ownerId), nên đây là nguồn dữ liệu
    /// duy nhất cần thiết thay vì hợp nhất thủ công từ Payment/Donation/Settlement riêng lẻ.
    /// </summary>
    Task<PaginatedResult<OwnerTransactionDto>> GetOwnerHistoryAsync(
        int ownerId, string? referenceType, DateTimeOffset? from, DateTimeOffset? to,
        int page, int pageSize, CancellationToken ct = default);
}

public sealed record JournalBalance(string JournalId, decimal DebitTotal, decimal CreditTotal);
