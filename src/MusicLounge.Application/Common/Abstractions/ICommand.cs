using MediatR;

namespace MusicLounge.Application.Common.Abstractions;

public interface ICommand<TResponse> : IRequest<TResponse> { }
public interface ICommand : ICommand<Unit> { }
