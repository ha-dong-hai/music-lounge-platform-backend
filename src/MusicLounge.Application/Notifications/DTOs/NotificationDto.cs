using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.Notifications.DTOs;

public sealed record NotificationDto(
    int Id,
    NotificationType Type,
    string Title,
    string Body,
    string? ReferenceType,
    string? ReferenceId,
    bool IsRead,
    DateTimeOffset CreatedAt);
