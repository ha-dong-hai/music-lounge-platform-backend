using MusicLounge.Application.Common;
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
/// <item><b>GMV</b> = tổng tiền người mua trả, trừ tiền đã hoàn.</item>
/// <item><b>Phần nền tảng thực nhận</b> = phần nền tảng hưởng của từng thanh toán theo <see cref="PlatformRevenue"/>
/// (đọc phí đã CHỐT trên thanh toán lúc mua, không tính lại từ tỉ lệ hoa hồng hiện tại), trừ phần phí của tiền đã hoàn.
/// MLACP-616 sửa câu cũ ghi "lấy từ bút toán sổ cái" — mã chưa bao giờ đọc sổ cái ở đây.</item>
/// </list>
/// <b>Hoàn tiền (MLACP-616):</b> doanh số ghi ở ngày bán, khoản hoàn ghi giảm trừ ở ngày duyệt hoàn — xem
/// <see cref="PlatformRevenue.BienDongAsync"/>.
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
        var (donVi, chuoi) = await ChuoiTheoKyAsync(from, to, ct);

        return new AdminDashboardDto(from, to, months, topShows, genres, donVi, chuoi);
    }

    // ---------- MLACP-594: tiền theo ĐÚNG khoảng đã chọn ----------

    /// <summary>Khoảng tới mức này thì gộp theo ngày; dài hơn tới <see cref="TranTuan"/> thì theo tuần; dài hơn nữa theo
    /// tháng. Cùng cách Stripe tự đổi đơn vị theo độ dài khoảng: 30 ngày thì 30 cột ngày, nửa năm thì ~26 cột tuần, một
    /// năm thì 12 cột tháng — biểu đồ không bao giờ quá ~31 cột, kể cả khi chọn nhiều năm (60 cột cho 5 năm là trần).</summary>
    internal const int TranNgay = 31;
    internal const int TranTuan = 184;

    internal static string DonViCho(DateTimeOffset from, DateTimeOffset to)
    {
        var soNgay = (to - from).TotalDays;
        return soNgay <= TranNgay ? "day" : soNgay <= TranTuan ? "week" : "month";
    }

    /// <summary>Đầu nhóm (giờ VN) chứa thời điểm <paramref name="luc"/>. Tuần bắt đầu thứ Hai (ISO 8601, thói quen ở VN).</summary>
    internal static DateTimeOffset DauNhom(DateTimeOffset luc, string donVi)
    {
        var vn = luc.ToOffset(VnOffset);
        var ngay = new DateTimeOffset(vn.Year, vn.Month, vn.Day, 0, 0, 0, VnOffset);
        return donVi switch
        {
            "day" => ngay,
            "week" => ngay.AddDays(-(((int)ngay.DayOfWeek + 6) % 7)),
            _ => new DateTimeOffset(vn.Year, vn.Month, 1, 0, 0, 0, VnOffset)
        };
    }

    private static DateTimeOffset NhomKe(DateTimeOffset dau, string donVi) => donVi switch
    {
        "day" => dau.AddDays(1),
        "week" => dau.AddDays(7),
        _ => dau.AddMonths(1)
    };

    private async Task<(string, IReadOnlyList<RevenueBucketDto>)> ChuoiTheoKyAsync(
        DateTimeOffset from, DateTimeOffset to, CancellationToken ct)
    {
        var donVi = DonViCho(from, to);
        var bienDong = (await BienDongTienAsync(ct)).Where(b => b.Luc >= from && b.Luc <= to);

        var theoNhom = bienDong
            .GroupBy(b => (Dau: DauNhom(b.Luc, donVi), b.Nguon))
            .ToDictionary(g => g.Key, g => new RevenueBySourceDto(g.Sum(b => b.Gmv), g.Sum(b => b.ThucNhan)));
        RevenueBySourceDto Khoi(DateTimeOffset dau, string nguon) =>
            theoNhom.GetValueOrDefault((dau, nguon)) ?? new RevenueBySourceDto(0m, 0m);

        // Sinh đủ mọi nhóm kể cả nhóm không có giao dịch — cùng lý do với khối 6 tháng: cột thiếu bị đọc nhầm là "chưa
        // có dữ liệu" thay vì "không bán được gì". Nhóm đầu/cuối có thể chỉ trọn một phần (khoảng cắt giữa tuần/tháng).
        var chuoi = new List<RevenueBucketDto>();
        for (var dau = DauNhom(from, donVi); dau <= to; dau = NhomKe(dau, donVi))
            chuoi.Add(new RevenueBucketDto(dau, Khoi(dau, NguonVe), Khoi(dau, NguonGoi), Khoi(dau, NguonDonate)));
        return (donVi, chuoi);
    }

    // ---------- khối 1+2: tiền theo tháng, tách nguồn ----------

    private async Task<IReadOnlyList<MonthlyRevenueDto>> SauThangGanNhatAsync(
        DateTimeOffset nowVn, CancellationToken ct)
    {
        var thangDau = new DateTimeOffset(nowVn.Year, nowVn.Month, 1, 0, 0, 0, VnOffset)
            .AddMonths(-(SoThangHienThi - 1));

        var bienDong = (await BienDongTienAsync(ct)).Where(b => b.Luc >= thangDau).ToList();



        string Thang(DateTimeOffset luc) => luc.ToOffset(VnOffset).ToString("yyyy-MM");

        var gmv = bienDong
            .GroupBy(b => (Thang: Thang(b.Luc), b.Nguon))
            .ToDictionary(g => g.Key, g => g.Sum(b => b.Gmv));

        // Phần nền tảng THỰC NHẬN, theo định nghĩa dùng chung ở PlatformRevenue: hoa hồng, KHÔNG gồm tiền giữ hộ chủ
        // phòng trà đang nằm tạm ở tài khoản nền tảng chờ quyết toán. Cộng cả tiền giữ hộ vào đây sẽ nói với người đọc
        // rằng nền tảng ăn gần trọn mỗi vé.
        var thucNhan = bienDong
            .GroupBy(b => (Thang: Thang(b.Luc), b.Nguon))
            .ToDictionary(g => g.Key, g => g.Sum(b => b.ThucNhan));

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

    /// <summary>MLACP-616: biến động tiền của ba nguồn trang này hiển thị — đọc từ định nghĩa dùng chung
    /// <see cref="PlatformRevenue.BienDongAsync"/>, cùng nguồn với thẻ tổng quan.</summary>
    private async Task<IReadOnlyList<(DateTimeOffset Luc, string Nguon, decimal Gmv, decimal ThucNhan)>> BienDongTienAsync(
        CancellationToken ct)
        => (await PlatformRevenue.BienDongAsync(_uow, ct))
            .Where(b => NguonCua(b.ThanhToan) is not null)
            .Select(b => (b.Luc, NguonCua(b.ThanhToan)!, b.Gmv, b.ThucNhan))
            .ToList();

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
        var giaTheoMa = (await _uow.Repository<TicketPrice, Guid>().FindAsync(p => maGia.Contains(p.Id), ct))
            .ToDictionary(p => p.Id, p => p.Price);
        decimal TienVe(Ticket t) => giaTheoMa.GetValueOrDefault(t.PriceId);

        var maShow = ve.Select(t => t.ShowId).Distinct().ToList();
        var shows = (await _uow.Repository<LoungeShow, Guid>().FindAsync(s => maShow.Contains(s.Id), ct))
            .ToDictionary(s => s.Id);

        var maPhongTra = shows.Values.Select(s => s.LoungeId).Distinct().ToList();
        var tenPhongTra = (await _uow.Repository<Domain.Entities.MusicLounge, Guid>()
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
        var lienKetTheLoai = await _uow.Repository<LoungeShowGenre, Guid>()
            .FindAsync(g => maShow.Contains(g.LoungeShowId), ct);

        var veTheoShow = ve.GroupBy(t => t.ShowId).ToDictionary(g => g.Key, g => g.Count());
        var maTheLoai = lienKetTheLoai.Select(l => l.GenreId).Distinct().ToList();
        var tenTheLoai = (await _uow.Repository<MusicGenre, Guid>().FindAsync(g => maTheLoai.Contains(g.Id), ct))
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
