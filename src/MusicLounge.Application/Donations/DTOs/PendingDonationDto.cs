namespace MusicLounge.Application.Donations.DTOs;

public sealed record PendingDonationDto(
    int Id,
    string PerformerName,
    string ShowName,
    decimal Gross,
    decimal Net,
    decimal AmountToPayPerformer,
    bool IsAnonymous,
    string? DisplayName,
    string? Message,
    DateTimeOffset? PaymentConfirmedAt,
    DateTimeOffset? AutoConfirmDeadline);
