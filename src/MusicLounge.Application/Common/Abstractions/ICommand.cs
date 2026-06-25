// CoreFlow: All — marker interfaces that distinguish write operations from read operations.
// Pipeline behaviors use IBaseCommand to apply transactions only to commands, never to queries.
using MediatR;

namespace MusicLounge.Application.Common.Abstractions;

// Shared marker so TransactionBehavior can identify any command without knowing the return type
public interface IBaseCommand { }

// Command that returns a value (e.g. CreateShowCommand → returns new ShowId)
public interface ICommand<TResponse> : IRequest<TResponse>, IBaseCommand { }

// Command that returns nothing (e.g. CancelShowCommand → void)
public interface ICommand : IRequest<Unit>, IBaseCommand { }
