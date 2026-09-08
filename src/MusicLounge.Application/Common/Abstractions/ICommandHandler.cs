// CoreFlow: All — typed handler interfaces for commands.
// Using these instead of raw IRequestHandler<,> makes handler intent explicit at a glance
// and constrains the type system so a command handler cannot accidentally handle a query.
using MediatR;

namespace MusicLounge.Application.Common.Abstractions;

// Handler for commands that return a value
public interface ICommandHandler<TCommand, TResponse> : IRequestHandler<TCommand, TResponse>
    where TCommand : ICommand<TResponse> { }

// Handler for commands that return nothing
public interface ICommandHandler<TCommand> : IRequestHandler<TCommand, Unit>
    where TCommand : ICommand { }
