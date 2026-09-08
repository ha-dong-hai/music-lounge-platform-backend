using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Notifications.Commands.UnregisterDeviceToken;

public sealed record UnregisterDeviceTokenCommand(string Token) : ICommand;
