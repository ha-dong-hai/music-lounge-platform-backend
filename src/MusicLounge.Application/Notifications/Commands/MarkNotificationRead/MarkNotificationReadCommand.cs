using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Notifications.Commands.MarkNotificationRead;

public sealed record MarkNotificationReadCommand(int NotificationId) : ICommand;
