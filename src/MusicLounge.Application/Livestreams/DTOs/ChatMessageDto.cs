namespace MusicLounge.Application.Livestreams.DTOs;

public sealed record ChatMessageDto(
    Guid MessageId,
    Guid UserId,
    string DisplayName,
    string Message,
    DateTimeOffset SentAt);
