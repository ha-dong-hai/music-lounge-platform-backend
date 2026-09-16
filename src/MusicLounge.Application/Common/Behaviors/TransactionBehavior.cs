using MediatR;
using MusicLounge.Application.Common.Abstractions;
using MusicLounge.Application.Common.Interfaces;

namespace MusicLounge.Application.Common.Behaviors;

internal sealed class TransactionBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : ICommand<TResponse>
{
    private readonly IUnitOfWork _uow;
    private readonly ITransactionLockScope _locks;

    public TransactionBehavior(IUnitOfWork uow, ITransactionLockScope locks)
    {
        _uow = uow;
        _locks = locks;
    }

    public async Task<TResponse> Handle(
        TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        if (request is INoTransactionCommand)
            return await next();

        // MLACP-396: moi khoa handler lay tu day duoc giu toi khi commit/rollback xong — xem ITransactionLockScope.
        _locks.Begin();
        try
        {
            // MLACP-415: transaction phai nam BEN TRONG execution strategy. Azure SQL reset ket noi vai lan moi ngay
            // (log 14-15/09); khong co lop nay thi moi lan nhu vay la mot loi 500 giua chung mot lenh, con bat
            // EnableRetryOnFailure ma van mo transaction ben ngoai thi EF nem thang loi cau hinh.
            return await _uow.ExecuteWithRetryAsync(async token =>
            {
                await _uow.BeginTransactionAsync(token);
                try
                {
                    var response = await next();
                    await _uow.CommitTransactionAsync(token);
                    return response;
                }
                catch
                {
                    await _uow.RollbackTransactionAsync(token);
                    throw;
                }
            }, ct);
        }
        finally
        {
            await _locks.EndAsync();
        }
    }
}
