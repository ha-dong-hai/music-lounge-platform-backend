using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Lounges.Commands.SetLoungeImage;

public sealed record SetLoungeImageCommand(Guid LoungeId, string ImageUrl) : ICommand;
