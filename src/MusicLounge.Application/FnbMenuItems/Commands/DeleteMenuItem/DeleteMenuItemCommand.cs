using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.FnbMenuItems.Commands.DeleteMenuItem;

public sealed record DeleteMenuItemCommand(Guid MenuItemId) : ICommand;
