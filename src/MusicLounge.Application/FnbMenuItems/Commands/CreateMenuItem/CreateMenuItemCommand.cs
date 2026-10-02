using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.FnbMenuItems.Commands.CreateMenuItem;

public sealed record CreateMenuItemCommand(
    Guid MenuId,
    string Category,
    string Name,
    string? Description,
    decimal Price,
    string? ImageUrl,
    int DisplayOrder
) : ICommand<Guid>;
