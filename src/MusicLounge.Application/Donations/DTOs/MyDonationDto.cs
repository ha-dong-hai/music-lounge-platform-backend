namespace MusicLounge.Application.Donations.DTOs;

public sealed record MyDonationDto(
    int Id,
    string PerformerName,
    string ShowName,
    decimal Gross,
    string Status,
    bool IsAnonymous,
    string? Message,
    DateTimeOffset? PaymentConfirmedAt,
    DateTimeOffset CreatedAt);
