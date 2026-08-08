using MediatR;

namespace MusicLounge.Application.Common.Abstractions;

public interface ICommand<TResponse> : IRequest<TResponse> { }
public interface ICommand : ICommand<Unit> { }

/// <summary>
/// Opts a command out of TransactionBehavior's ambient transaction. Needed by any command whose
/// handler writes through IAuthAttemptTracker (or another connection independent of the ambient
/// IUnitOfWork) — that write must commit immediately so it survives a failure the handler itself
/// goes on to throw, but SQLite (the test provider) single-writer-locks the whole database while
/// any transaction is open, so a second connection's write deadlocks/times out for as long as this
/// command's own transaction stays open. Only opt out commands, like Login/VerifyEmail, that don't
/// otherwise need cross-statement atomicity — EF Core still wraps each individual SaveChangesAsync
/// call in its own implicit transaction regardless.
/// </summary>
public interface INoTransactionCommand { }
