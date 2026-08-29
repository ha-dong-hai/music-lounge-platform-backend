namespace MusicLounge.Application.Users.DTOs;

public sealed record OwnerTransactionDto(
    int Id,
    string Type,
    string ReferenceId,
    decimal Amount,
    string? Description,
    DateTimeOffset CreatedAt);
