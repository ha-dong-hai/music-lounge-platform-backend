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
            await _uow.BeginTransactionAsync(ct);
            try
            {
                var response = await next();
                await _uow.CommitTransactionAsync(ct);
                return response;
            }
            catch
            {
                await _uow.RollbackTransactionAsync(ct);
                throw;
            }
        }
        finally
        {
            await _locks.EndAsync();
        }
    }
}
