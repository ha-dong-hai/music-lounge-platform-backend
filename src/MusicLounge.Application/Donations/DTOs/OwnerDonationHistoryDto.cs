using MusicLounge.Application.Common.Models;

namespace MusicLounge.Application.Donations.DTOs;

public sealed record OwnerDonationHistoryItemDto(
    int Id,
    string PerformerName,
    string ShowName,
    decimal Gross,
    decimal Net,
    string PayoutStatus,
    DateTimeOffset? PaymentConfirmedAt,
    DateTimeOffset? OwnerPaidAt,
    DateTimeOffset? PayoutDueAt,
    DateTimeOffset CreatedAt);

public sealed record OwnerDonationHistorySummaryDto(
    DateTimeOffset PeriodFrom,
    DateTimeOffset PeriodTo,
    int TotalCount,
    decimal TotalGross,
    int PaidCount,
    int WithinHoldCount,
    int OverdueCount,
    PaginatedResult<OwnerDonationHistoryItemDto> Items);
