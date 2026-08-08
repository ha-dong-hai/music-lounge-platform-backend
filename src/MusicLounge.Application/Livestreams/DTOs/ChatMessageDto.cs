namespace MusicLounge.Application.Livestreams.DTOs;

public sealed record ChatMessageDto(
    int MessageId,
    int UserId,
    string DisplayName,
    string Message,
    DateTimeOffset SentAt);
