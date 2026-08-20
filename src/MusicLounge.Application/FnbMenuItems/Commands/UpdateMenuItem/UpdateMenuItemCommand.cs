using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.FnbMenuItems.Commands.UpdateMenuItem;

public sealed record UpdateMenuItemCommand(
    int MenuItemId,
    string Category,
    string Name,
    string? Description,
    decimal Price,
    string? ImageUrl,
    bool IsAvailable,
    int DisplayOrder
) : ICommand;
