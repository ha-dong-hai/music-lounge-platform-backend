// CoreFlow: All — typed handler interface for queries.
// Mirrors ICommandHandler — symmetry makes the codebase easier to navigate.
using MediatR;

namespace MusicLounge.Application.Common.Abstractions;

public interface IQueryHandler<TQuery, TResponse> : IRequestHandler<TQuery, TResponse>
    where TQuery : IQuery<TResponse> { }
