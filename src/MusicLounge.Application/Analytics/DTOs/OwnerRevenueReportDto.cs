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
    // MLACP-207: "quyết toán đã nhận" — tổng NetAmount các tranche Settlement đã Released (đã về
    // tài khoản ngân hàng Owner) trong kỳ, theo ReleasedAt. Khác GrandTotal (doanh thu gộp phát
    // sinh trong kỳ) — đây là tiền THẬT SỰ đã nhận, có thể lệch kỳ với lúc phát sinh doanh thu vì
    // settlement giải ngân sau show (D3, 2 đợt).
    decimal TotalSettlementReceived,
    // "phí nền tảng đã trả" — phần chênh lệch GrossAmount - NetAmount của các settlement Released
    // cùng kỳ (hoa hồng + thuế platform đã khấu trừ thật khi giải ngân).
    decimal TotalPlatformFeePaid,
    IReadOnlyList<RevenueByEventDto> ByEvent,
    IReadOnlyList<RevenueByMonthDto> ByMonth);
