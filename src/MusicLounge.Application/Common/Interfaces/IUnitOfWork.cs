using MusicLounge.Domain.Common;

namespace MusicLounge.Application.Common.Interfaces;

public interface IUnitOfWork : IDisposable
{
    IRepository<T, TKey> Repository<T, TKey>() where T : BaseEntity<TKey>;
    Task<int> SaveChangesAsync(CancellationToken ct = default);
    Task BeginTransactionAsync(CancellationToken ct = default);
    Task CommitTransactionAsync(CancellationToken ct = default);
    Task RollbackTransactionAsync(CancellationToken ct = default);
}
