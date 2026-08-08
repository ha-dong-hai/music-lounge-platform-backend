using System.Linq.Expressions;
using MusicLounge.Domain.Common;

namespace MusicLounge.Application.Common.Interfaces;

public interface IRepository<T, TKey> where T : BaseEntity<TKey>
{
    Task<T?> GetByIdAsync(TKey id, CancellationToken ct = default);
    Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default);
    Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default);
    Task<int> CountAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default);

    /// <summary>
    /// Projects to just <paramref name="selector"/> before summing, instead of the FindAsync-then-
    /// .Sum() pattern that materializes every matched row's full column set into memory for a
    /// single aggregate value.
    /// </summary>
    Task<decimal> SumAsync(
        Expression<Func<T, bool>> predicate, Expression<Func<T, decimal>> selector, CancellationToken ct = default);

    /// <summary>
    /// Orders, counts, and pages server-side — the alternative (FindAsync the full matching set,
    /// then OrderBy/Skip/Take/Count in C#) pulls every matching row into memory just to throw most
    /// of them away, and gets worse every time more rows accumulate (a pending-request queue, a
    /// venue's order history) instead of staying bounded by page size.
    /// </summary>
    Task<(IReadOnlyList<T> Items, int TotalCount)> GetPagedAsync<TOrderKey>(
        Expression<Func<T, bool>> predicate,
        Expression<Func<T, TOrderKey>> orderByDescending,
        int page,
        int pageSize,
        CancellationToken ct = default);

    void Add(T entity);
    void AddRange(IEnumerable<T> entities);
    void Update(T entity);
    void Remove(T entity);
}
