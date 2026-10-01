using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Lounges.Commands.DeleteLounge;

public sealed record DeleteLoungeCommand(Guid LoungeId) : ICommand;
