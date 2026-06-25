// CoreFlow: All — third behavior in the pipeline; wraps every command in a DB transaction.
// Queries are skipped — read operations never mutate state so a transaction would be wasteful.
// On any unhandled exception the transaction rolls back, preventing partial writes.
using MediatR;
using MusicLounge.Application.Common.Abstractions;
using MusicLounge.Application.Common.Interfaces;

namespace MusicLounge.Application.Common.Behaviors;

public class TransactionBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IUnitOfWork _unitOfWork;

    public TransactionBehavior(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        // IBaseCommand is the shared marker for both ICommand and ICommand<TResponse>
        if (request is not IBaseCommand)
            return await next();

        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            var response = await next();
            await _unitOfWork.CommitAsync(cancellationToken);
            return response;
        }
        catch
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
