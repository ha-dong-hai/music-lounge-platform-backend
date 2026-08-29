using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Notifications.Commands.RegisterDeviceToken;

public sealed record RegisterDeviceTokenCommand(string Token, string? Platform) : ICommand;
