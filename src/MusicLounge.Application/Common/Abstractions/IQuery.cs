using MediatR;

namespace MusicLounge.Application.Common.Abstractions;

public interface IQuery<TResponse> : IRequest<TResponse> { }
