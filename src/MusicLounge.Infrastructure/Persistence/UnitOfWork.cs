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

    // ApplicationDbContext is independently registered (AddDbContext, Scoped) and never
    // constructed/owned by UnitOfWork — the DI container disposes it on its own at scope end.
    // Disposing it here too used to be harmless only because DbContext.Dispose() is idempotent;
    // it's still the wrong owner disposing it, and a latent risk if UnitOfWork is ever
    // manually scoped (e.g. `using var uow = ...`) while some other still-running scoped service
    // holds a reference to the same context. Only dispose what this class actually created itself.
    public void Dispose() => _transaction?.Dispose();
}
