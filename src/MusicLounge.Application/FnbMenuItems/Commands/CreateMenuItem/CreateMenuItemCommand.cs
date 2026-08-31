using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.FnbMenuItems.Commands.CreateMenuItem;

public sealed record CreateMenuItemCommand(
    int MenuId,
    string Category,
    string Name,
    string? Description,
    decimal Price,
    string? ImageUrl,
    int DisplayOrder
) : ICommand<int>;
