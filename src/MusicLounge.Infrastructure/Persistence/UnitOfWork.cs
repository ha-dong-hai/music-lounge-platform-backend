using Microsoft.EntityFrameworkCore.Storage;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Common;

namespace MusicLounge.Infrastructure.Persistence;

internal sealed class UnitOfWork : IUnitOfWork
{
    private readonly ApplicationDbContext _ctx;
    private IDbContextTransaction? _transaction;

    public UnitOfWork(ApplicationDbContext ctx) => _ctx = ctx;

    public IRepository<T, TKey> Repository<T, TKey>() where T : BaseEntity<TKey>
        => new Repository<T, TKey>(_ctx);

    public Task<int> SaveChangesAsync(CancellationToken ct = default)
        => _ctx.SaveChangesAsync(ct);

    public async Task BeginTransactionAsync(CancellationToken ct = default)
        => _transaction = await _ctx.Database.BeginTransactionAsync(ct);

    public async Task CommitTransactionAsync(CancellationToken ct = default)
    {
        if (_transaction is null) return;
        await _transaction.CommitAsync(ct);
        await _transaction.DisposeAsync();
        _transaction = null;
    }

    public async Task RollbackTransactionAsync(CancellationToken ct = default)
    {
        if (_transaction is null) return;
        await _transaction.RollbackAsync(ct);
        await _transaction.DisposeAsync();
        _transaction = null;
    }

    public void Dispose()
    {
        _transaction?.Dispose();
        _ctx.Dispose();
    }
}
