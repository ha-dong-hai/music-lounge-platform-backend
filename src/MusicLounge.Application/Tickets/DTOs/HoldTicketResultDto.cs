namespace MusicLounge.Application.Tickets.DTOs;

public sealed record HoldTicketResultDto(Guid HoldId, DateTimeOffset ExpiresAt);
