using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Lounges.Commands.ReorderLoungeGalleryImages;

// Vi tri trong OrderedImageIds chinh la OrderIndex moi (phan tu dau = 0).
public sealed record ReorderLoungeGalleryImagesCommand(int LoungeId, List<int> OrderedImageIds) : ICommand;
