using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.FnbMenus.Commands.DeleteFnbMenu;

public sealed record DeleteFnbMenuCommand(Guid MenuId) : ICommand;
