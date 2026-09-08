using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Lounges.Commands.DeleteLounge;

public sealed record DeleteLoungeCommand(int LoungeId) : ICommand;
