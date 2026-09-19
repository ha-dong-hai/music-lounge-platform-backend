using MediatR;
using MusicLounge.Application.Analytics.DTOs;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.Analytics.Queries.GetAdminDashboard;

/// <summary>
/// MLACP-463. Bốn khối của trang tổng quan Admin.
///
/// <b>Hai con số tiền, cố ý tách riêng chứ không gộp thành một chữ "doanh thu":</b>
/// <list type="bullet">
/// <item><b>GMV</b> = tổng tiền người mua trả, lấy từ các khoản thanh toán đã xác nhận.</item>
/// <item><b>Phần nền tảng thực nhận</b> = bút toán ghi CÓ vào tài khoản nền tảng trong SỔ CÁI — không tính lại từ tỉ lệ
/// hoa hồng, vì tỉ lệ đổi theo thời gian còn sổ cái ghi đúng thứ đã xảy ra. Cùng cách <c>/analytics/admin-overview</c>
/// đang tính, nên hai màn hình không bao giờ lệch nhau.</item>
/// </list>
/// Gộp hai con số này làm một là chỗ dễ nói dối nhất của mọi trang tổng quan: bán 100 triệu tiền vé không có nghĩa nền
/// tảng thu 100 triệu.
///
/// <b>Vé bán tại quầy bằng tiền mặt</b> có trong GMV nhưng gần như không có trong phần nền tảng nhận: phòng trà thu trực
/// tiếp, mặc định không qua sổ cái. Đó là thật, không phải lỗi số liệu.
///
/// <b>Lọc ở ứng dụng, không ở cơ sở dữ liệu:</b> gộp so sánh <c>DateTimeOffset</c> với điều kiện khác trong cùng một
/// truy vấn không dịch được dưới provider SQLite của bộ test — cùng giới hạn đã ghi ở nhiều nơi khác trong dự án.
/// Trần giới hạn: hàm nạp toàn bộ thanh toán đã xác nhận và bút toán của nền tảng. Ở quy mô đồ án thì rẻ; khi số giao
/// dịch lên hàng trăm nghìn, đường nâng cấp là gộp sẵn theo tháng vào một bảng tổng hợp do job định kỳ ghi.
/// </summary>
internal sealed class GetAdminDashboardQueryHandler
    : IRequestHandler<GetAdminDashboardQuery, AdminDashboardDto>
{
    /// <summary>Cùng quy ước giờ Việt Nam với các handler phân tích khác.</summary>
    private static readonly TimeSpan VnOffset = TimeSpan.FromHours(7);

    private const int SoThangHienThi = 6;

    /// <summary>Khoản thanh toán thuộc nguồn nào. Vé gồm cả mua online lẫn mua tại quầy.</summary>
    private const string NguonVe = "ticket";
    private const string NguonGoi = "package";
    private const string NguonDonate = "donation";

    private readonly IUnitOfWork _uow;

    public GetAdminDashboardQueryHandler(IUnitOfWork uow) => _uow = uow;

    public async Task<AdminDashboardDto> Handle(GetAdminDashboardQuery request, CancellationToken ct)
    {
        var nowVn = DateTimeOffset.UtcNow.ToOffset(VnOffset);
        var to = request.To ?? nowVn;
        var from = request.From ?? new DateTimeOffset(nowVn.Year, nowVn.Month, 1, 0, 0, 0, VnOffset)
            .AddMonths(-(SoThangHienThi - 1));
        var limit = Math.Clamp(request.Limit, 1, 50);

        var months = await SauThangGanNhatAsync(nowVn, ct);
        var (topShows, genres) = await TopVaTheLoaiAsync(from, to, limit, ct);

        return new AdminDashboardDto(from, to, months, topShows, genres);
    }

    // ---------- khối 1+2: tiền theo tháng, tách nguồn ----------

    private async Task<IReadOnlyList<MonthlyRevenueDto>> SauThangGanNhatAsync(
        DateTimeOffset nowVn, CancellationToken ct)
    {
        var thangDau = new DateTimeOffset(nowVn.Year, nowVn.Month, 1, 0, 0, 0, VnOffset)
            .AddMonths(-(SoThangHienThi - 1));

        var thanhToan = (await _uow.Repository<Payment, int>()
                .FindAsync(p => p.Status == PaymentStatus.Confirmed, ct))
            .Where(p => p.PaidAt.HasValue && p.PaidAt.Value >= thangDau)
            .ToList();

        // Bút toán ghi CÓ vào tài khoản nền tảng, gắn với một khoản thanh toán — PaymentId là đường duy nhất biết bút
        // toán này sinh ra từ nguồn nào (bản thân LedgerEntry.ReferenceType chỉ có "payment"/"settlement"/"donation"/
        // "refund", không phân biệt vé với gói dịch vụ).
        var butToanNenTang = (await _uow.Repository<LedgerEntry, int>()
                .FindAsync(e => e.Account.OwnerType == AccountType.Platform && !e.IsDebit, ct))
            .Where(e => e.CreatedAt >= thangDau && e.PaymentId.HasValue)
            .ToList();

        var nguonTheoThanhToan = thanhToan.ToDictionary(p => p.Id, NguonCua);

        string Thang(DateTimeOffset luc) => luc.ToOffset(VnOffset).ToString("yyyy-MM");

        var gmv = thanhToan
            .Where(p => NguonCua(p) is not null)
            .GroupBy(p => (Thang: Thang(p.PaidAt!.Value), Nguon: NguonCua(p)!))
            .ToDictionary(g => g.Key, g => g.Sum(p => p.GrossAmount));

        var thucNhan = butToanNenTang
            .Where(e => nguonTheoThanhToan.GetValueOrDefault(e.PaymentId!.Value) is not null)
            .GroupBy(e => (Thang: Thang(e.CreatedAt), Nguon: nguonTheoThanhToan[e.PaymentId!.Value]!))
            .ToDictionary(g => g.Key, g => g.Sum(e => e.Amount));

        RevenueBySourceDto Khoi(string thang, string nguon) => new(
            gmv.GetValueOrDefault((thang, nguon)),
            thucNhan.GetValueOrDefault((thang, nguon)));

        // Sinh đủ 6 tháng kể cả tháng không có giao dịch nào: biểu đồ thiếu cột sẽ bị đọc nhầm thành "tháng đó chưa có
        // dữ liệu" thay vì "tháng đó không bán được gì".
        return Enumerable.Range(0, SoThangHienThi)
            .Select(i => thangDau.AddMonths(i).ToString("yyyy-MM"))
            .Select(thang => new MonthlyRevenueDto(
                thang,
                Khoi(thang, NguonVe),
                Khoi(thang, NguonGoi),
                Khoi(thang, NguonDonate)))
            .ToList();
    }

    /// <summary>
    /// Nguồn của một khoản thanh toán. <c>null</c> = không thuộc ba nguồn trang này hiển thị (hiện chỉ có đơn gọi món —
    /// cố ý không gộp vào đâu cả, vì gộp nó vào "vé" sẽ làm doanh thu vé cao hơn sự thật).
    /// </summary>
    private static string? NguonCua(Payment p) => p.ReferenceType switch
    {
        "TicketHold" or "WalkIn" => NguonVe,
        "Subscription" => NguonGoi,
        "Donation" => NguonDonate,
        _ => null
    };

    // ---------- khối 3+4: top buổi hòa nhạc và thể loại ----------

    private async Task<(IReadOnlyList<TopShowRevenueDto>, IReadOnlyList<GenreDemandDto>)> TopVaTheLoaiAsync(
        DateTimeOffset from, DateTimeOffset to, int limit, CancellationToken ct)
    {
        // "Đã bán" = Confirmed hoặc Used (soát vé rồi vẫn là vé đã bán). Cancelled/Refunded không tính vào doanh thu —
        // cùng định nghĩa với GetTicketSalesTrend và GetShowPerformance.
        var ve = (await _uow.Repository<Ticket, Guid>().FindAsync(
                t => t.Status == TicketStatus.Confirmed || t.Status == TicketStatus.Used, ct))
            .Where(t => t.CreatedAt >= from && t.CreatedAt <= to)
            .ToList();

        if (ve.Count == 0) return ([], []);

        var maGia = ve.Select(t => t.PriceId).Distinct().ToList();
        var giaTheoMa = (await _uow.Repository<TicketPrice, int>().FindAsync(p => maGia.Contains(p.Id), ct))
            .ToDictionary(p => p.Id, p => p.Price);
        decimal TienVe(Ticket t) => giaTheoMa.GetValueOrDefault(t.PriceId);

        var maShow = ve.Select(t => t.ShowId).Distinct().ToList();
        var shows = (await _uow.Repository<LoungeShow, int>().FindAsync(s => maShow.Contains(s.Id), ct))
            .ToDictionary(s => s.Id);

        var maPhongTra = shows.Values.Select(s => s.LoungeId).Distinct().ToList();
        var tenPhongTra = (await _uow.Repository<Domain.Entities.MusicLounge, int>()
                .FindAsync(l => maPhongTra.Contains(l.Id), ct))
            .ToDictionary(l => l.Id, l => l.Name);

        var topShows = ve
            .GroupBy(t => t.ShowId)
            .Where(g => shows.ContainsKey(g.Key))
            .Select(g => new TopShowRevenueDto(
                g.Key,
                shows[g.Key].Name,
                tenPhongTra.GetValueOrDefault(shows[g.Key].LoungeId, string.Empty),
                shows[g.Key].ScheduledStart,
                g.Count(),
                g.Sum(TienVe)))
            .OrderByDescending(x => x.TicketRevenue)
            .ThenBy(x => x.ShowId)
            .Take(limit)
            .ToList();

        // Một buổi hòa nhạc có thể mang nhiều thể loại: vé của nó được tính cho TỪNG thể loại. Cộng các cột lại sẽ lớn
        // hơn tổng số vé bán ra — đó là bản chất của biểu đồ này, không phải lỗi.
        var lienKetTheLoai = await _uow.Repository<LoungeShowGenre, int>()
            .FindAsync(g => maShow.Contains(g.LoungeShowId), ct);

        var veTheoShow = ve.GroupBy(t => t.ShowId).ToDictionary(g => g.Key, g => g.Count());
        var maTheLoai = lienKetTheLoai.Select(l => l.GenreId).Distinct().ToList();
        var tenTheLoai = (await _uow.Repository<MusicGenre, int>().FindAsync(g => maTheLoai.Contains(g.Id), ct))
            .ToDictionary(g => g.Id, g => g.Name);

        var genres = lienKetTheLoai
            .GroupBy(l => l.GenreId)
            .Select(g => new GenreDemandDto(
                g.Key,
                tenTheLoai.GetValueOrDefault(g.Key, string.Empty),
                g.Sum(l => veTheoShow.GetValueOrDefault(l.LoungeShowId)),
                g.Select(l => l.LoungeShowId).Distinct().Count()))
            .Where(g => g.TicketsSold > 0)
            .OrderByDescending(g => g.TicketsSold)
            .ThenBy(g => g.GenreId)
            .ToList();

        return (topShows, genres);
    }
}
