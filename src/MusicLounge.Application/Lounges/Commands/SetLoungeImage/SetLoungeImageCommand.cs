using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Lounges.Commands.SetLoungeImage;

public sealed record SetLoungeImageCommand(int LoungeId, string ImageUrl) : ICommand;
