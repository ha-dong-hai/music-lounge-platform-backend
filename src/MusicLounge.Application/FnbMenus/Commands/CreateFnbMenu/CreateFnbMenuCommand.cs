using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.FnbMenus.Commands.CreateFnbMenu;

public sealed record CreateFnbMenuCommand(
    Guid LoungeId,
    string Name,
    string? Description,
    int DisplayOrder
) : ICommand<Guid>;
