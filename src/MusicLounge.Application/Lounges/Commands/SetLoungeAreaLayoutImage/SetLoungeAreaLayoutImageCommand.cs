using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Lounges.Commands.SetLoungeAreaLayoutImage;

public sealed record SetLoungeAreaLayoutImageCommand(int LoungeId, string? ImageUrl) : ICommand;
