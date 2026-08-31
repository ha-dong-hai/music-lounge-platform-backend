namespace MusicLounge.Application.Analytics.DTOs;

public sealed record RevenueByEventDto(
    int ShowId,
    string ShowName,
    DateTimeOffset ScheduledStart,
    decimal TicketRevenue,
    decimal FnbRevenue,
    decimal DonationRevenue,
    decimal TotalRevenue);

public sealed record RevenueByMonthDto(
    int Year,
    int Month,
    decimal TicketRevenue,
    decimal FnbRevenue,
    decimal DonationRevenue,
    decimal TotalRevenue);

public sealed record OwnerRevenueReportDto(
    decimal TotalTicketRevenue,
    decimal TotalFnbRevenue,
    decimal TotalDonationRevenue,
    decimal GrandTotal,
    IReadOnlyList<RevenueByEventDto> ByEvent,
    IReadOnlyList<RevenueByMonthDto> ByMonth);
