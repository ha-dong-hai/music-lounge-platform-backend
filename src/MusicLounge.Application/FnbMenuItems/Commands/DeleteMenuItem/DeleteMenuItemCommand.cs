using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.FnbMenuItems.Commands.DeleteMenuItem;

public sealed record DeleteMenuItemCommand(int MenuItemId) : ICommand;
