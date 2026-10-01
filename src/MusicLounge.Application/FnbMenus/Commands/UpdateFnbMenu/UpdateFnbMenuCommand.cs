using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.FnbMenus.Commands.UpdateFnbMenu;

public sealed record UpdateFnbMenuCommand(
    Guid MenuId,
    string Name,
    string? Description,
    bool IsActive,
    int DisplayOrder
) : ICommand;
