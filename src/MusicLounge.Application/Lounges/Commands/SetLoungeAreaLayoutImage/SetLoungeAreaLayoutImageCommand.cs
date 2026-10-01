using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Lounges.Commands.SetLoungeAreaLayoutImage;

public sealed record SetLoungeAreaLayoutImageCommand(Guid LoungeId, string? ImageUrl) : ICommand;
