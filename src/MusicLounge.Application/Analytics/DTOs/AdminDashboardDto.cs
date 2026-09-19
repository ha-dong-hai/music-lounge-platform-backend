namespace MusicLounge.Application.Analytics.DTOs;

/// <summary>
/// MLACP-463. Bốn khối của trang tổng quan Admin. Trước task này giao diện có sẵn 4 khối biểu đồ nhưng không có endpoint
/// nào cấp dữ liệu, nên frontend đã phải gỡ hẳn chúng khỏi màn hình.
/// </summary>
/// <param name="Months">6 tháng gần nhất, tháng hiện tại là tháng chưa trọn. Theo giờ Việt Nam (UTC+7).</param>
/// <param name="TopShows">Buổi hòa nhạc có doanh thu vé cao nhất trong khoảng thời gian đã chọn.</param>
/// <param name="Genres">Thể loại nhạc được mua vé nhiều nhất trong khoảng thời gian đã chọn.</param>
public sealed record AdminDashboardDto(
    DateTimeOffset PeriodFrom,
    DateTimeOffset PeriodTo,
    IReadOnlyList<MonthlyRevenueDto> Months,
    IReadOnlyList<TopShowRevenueDto> TopShows,
    IReadOnlyList<GenreDemandDto> Genres);

/// <param name="Month">Dạng <c>yyyy-MM</c> theo giờ Việt Nam.</param>
public sealed record MonthlyRevenueDto(
    string Month,
    RevenueBySourceDto Ticket,
    RevenueBySourceDto Package,
    RevenueBySourceDto Donation);

/// <param name="Gmv">
/// Tổng tiền người mua trả (gross). Bao gồm cả vé bán tại quầy bằng tiền mặt — đó vẫn là giao dịch thật của nền tảng,
/// dù tiền không đi qua tài khoản của nền tảng.
/// </param>
/// <param name="PlatformRevenue">
/// Phần nền tảng thực nhận, lấy từ SỔ CÁI (bút toán ghi CÓ vào tài khoản nền tảng), không phải tính lại từ tỉ lệ hoa
/// hồng. Vé bán tại quầy mặc định không sinh bút toán nào, nên khoản này nhỏ hơn hẳn <paramref name="Gmv"/>.
/// </param>
public sealed record RevenueBySourceDto(decimal Gmv, decimal PlatformRevenue);

public sealed record TopShowRevenueDto(
    int ShowId,
    string Title,
    string LoungeName,
    DateTimeOffset StartTime,
    int TicketsSold,
    decimal TicketRevenue);

/// <param name="ShowCount">Số buổi hòa nhạc thuộc thể loại này có vé bán được trong khoảng thời gian đã chọn.</param>
public sealed record GenreDemandDto(
    int GenreId,
    string GenreName,
    int TicketsSold,
    int ShowCount);
