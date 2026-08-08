using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Common;

namespace MusicLounge.Infrastructure.Persistence;

internal class Repository<T, TKey> : IRepository<T, TKey>
    where T : BaseEntity<TKey>
{
    private readonly ApplicationDbContext _ctx;

    public Repository(ApplicationDbContext ctx) => _ctx = ctx;

    private DbSet<T> Set => _ctx.Set<T>();

    public async Task<T?> GetByIdAsync(TKey id, CancellationToken ct = default)
        => await Set.FindAsync([id], ct);

    public async Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default)
        => await Set.AsNoTracking().ToListAsync(ct);

    public async Task<IReadOnlyList<T>> FindAsync(
        Expression<Func<T, bool>> predicate, CancellationToken ct = default)
        => await Set.AsNoTracking().Where(predicate).ToListAsync(ct);

    public Task<bool> AnyAsync(
        Expression<Func<T, bool>> predicate, CancellationToken ct = default)
        => Set.AnyAsync(predicate, ct);

    public Task<int> CountAsync(
        Expression<Func<T, bool>> predicate, CancellationToken ct = default)
        => Set.CountAsync(predicate, ct);

    public async Task<decimal> SumAsync(
        Expression<Func<T, bool>> predicate, Expression<Func<T, decimal>> selector, CancellationToken ct = default)
    {
        var values = await Set.AsNoTracking().Where(predicate).Select(selector).ToListAsync(ct);
        return values.Sum();
    }

    public async Task<(IReadOnlyList<T> Items, int TotalCount)> GetPagedAsync<TOrderKey>(
        Expression<Func<T, bool>> predicate,
        Expression<Func<T, TOrderKey>> orderByDescending,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = Set.AsNoTracking().Where(predicate);
        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(orderByDescending)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
        return (items, total);
    }

    public void Add(T entity) => Set.Add(entity);

    public void AddRange(IEnumerable<T> entities) => Set.AddRange(entities);

    public void Update(T entity) => Set.Update(entity);

    public void Remove(T entity) => Set.Remove(entity);
}
