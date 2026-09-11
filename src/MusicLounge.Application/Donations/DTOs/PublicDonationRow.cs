using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.Donations.DTOs;

/// <summary>
/// MLACP-365 — dữ liệu thô của một khoản donate cho trang sao kê công khai. Không trả thẳng ra API:
/// <see cref="PublicDonationStatement"/> quyết định trường nào được công khai và công khai thế nào.
/// </summary>
public sealed record PublicDonationRow(
    int Id,
    string ShowName,
    string VenueName,
    DateTimeOffset ShowDate,
    string? DonorDisplayName,
    bool IsAmountPublic,
    decimal Gross,
    decimal Net,
    decimal? PerformerShareRateSnapshot,
    DonationStatus Status,
    bool AutoConfirmed,
    DateTimeOffset? PaymentConfirmedAt,
    DateTimeOffset? OwnerAckAt,
    DateTimeOffset? OwnerPaidAt,
    string? Message,
    bool IsMessagePublic,
    DateTimeOffset? MessageHiddenAt,
    bool HasTransferReceipt,
    DateTimeOffset CreatedAt
);
