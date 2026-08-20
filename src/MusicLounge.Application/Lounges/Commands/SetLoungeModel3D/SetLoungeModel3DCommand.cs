using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Lounges.Commands.SetLoungeModel3D;

public sealed record SetLoungeModel3DCommand(int LoungeId, string? ModelUrl) : ICommand;
