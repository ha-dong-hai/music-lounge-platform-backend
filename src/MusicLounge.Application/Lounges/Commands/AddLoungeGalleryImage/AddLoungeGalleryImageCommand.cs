using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Lounges.Commands.AddLoungeGalleryImage;

public sealed record AddLoungeGalleryImageCommand(
    int LoungeId,
    string ImageUrl,
    string? Caption
) : ICommand<int>;
