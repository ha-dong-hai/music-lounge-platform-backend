namespace MusicLounge.Application.Tickets.DTOs;

public sealed record HoldTicketResultDto(int HoldId, DateTimeOffset ExpiresAt);
