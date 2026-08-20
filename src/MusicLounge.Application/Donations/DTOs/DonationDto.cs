namespace MusicLounge.Application.Donations.DTOs;

public sealed record DonationDto(
    int Id,
    int PerformanceId,
    string PerformerName,
    string ShowName,
    decimal Gross,
    decimal Net,
    string Status,
    bool AutoConfirmed,
    DateTimeOffset? OwnerAckAt,
    DateTimeOffset? OwnerPaidAt,
    bool IsAnonymous,
    string? DisplayName,
    bool IsAmountPublic,
    string? Message,
    DateTimeOffset CreatedAt
);
