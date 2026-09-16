using MusicLounge.Domain.Common;

namespace MusicLounge.Application.Common.Interfaces;

public interface IUnitOfWork : IDisposable
{
    IRepository<T, TKey> Repository<T, TKey>() where T : BaseEntity<TKey>;
    Task<int> SaveChangesAsync(CancellationToken ct = default);
    Task BeginTransactionAsync(CancellationToken ct = default);
    Task CommitTransactionAsync(CancellationToken ct = default);
    Task RollbackTransactionAsync(CancellationToken ct = default);

    /// <summary>
    /// MLACP-415: chay <paramref name="operation"/> qua execution strategy cua EF Core. Voi Azure SQL (bat
    /// EnableRetryOnFailure) nghia la mot loi ket noi thoang qua se duoc thu lai TOAN BO thao tac ben trong, ke ca
    /// transaction — day cung la yeu cau bat buoc cua EF: mo transaction thu cong ben ngoai execution strategy se nem
    /// "The configured execution strategy 'SqlServerRetryingExecutionStrategy' does not support user-initiated
    /// transactions". Luu y: khi thu lai, moi thu trong <paramref name="operation"/> chay lai tu dau.
    /// </summary>
    Task<T> ExecuteWithRetryAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken ct = default);
}
