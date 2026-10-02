using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Lounges.Commands.RemoveLoungeGalleryImage;

public sealed record RemoveLoungeGalleryImageCommand(Guid LoungeId, Guid ImageId) : ICommand;
