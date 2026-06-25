// CoreFlow: All — abstracts the database transaction boundary.
// Application layer declares what it needs; Infrastructure (EF Core) implements it.
// This keeps the domain and application layers free of any EF Core dependency.
namespace MusicLounge.Application.Common.Interfaces;

public interface IUnitOfWork
{
    // Persist all tracked changes in the current session without a transaction
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    // Open an explicit transaction — called by TransactionBehavior before the handler runs
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);

    // Flush all changes and commit the open transaction atomically
    Task CommitAsync(CancellationToken cancellationToken = default);

    // Discard all changes and roll back — called automatically on any unhandled exception
    Task RollbackAsync(CancellationToken cancellationToken = default);
}
