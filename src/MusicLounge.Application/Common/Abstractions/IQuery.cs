// CoreFlow: All — marker interface for read-only operations.
// Queries never mutate state and are excluded from transaction wrapping.
using MediatR;

namespace MusicLounge.Application.Common.Abstractions;

public interface IQuery<TResponse> : IRequest<TResponse> { }
