using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.Settlements;

/// <summary>
/// Câu tiếng Việt cho chủ phòng trà biết một khoản tiền là GÌ: tiền vé buổi nào, đợt nào, mấy vé; tiền ủng hộ nghệ sĩ nào,
/// của ai. Dùng chung cho lịch sử giao dịch (MLACP-655) và danh sách quyết toán gần đây (MLACP-658) — hai màn trước đây
/// chỉ có số tiền hoặc chú thích nội bộ kèm mã GUID (chủ dự án 05/10/2026: "không hiểu số tiền đó là của giao dịch gì").
///
/// <para>Nạp một lần cho cả trang (mỗi bảng một truy vấn, không truy vấn theo từng dòng). Thiếu dữ liệu nguồn (bản ghi đã
/// xoá hoặc ẩn danh hoá) thì rơi về câu chung — không bao giờ in GUID.</para>
/// </summary>
public sealed class OwnerMoneyTitles
{
    private readonly Dictionary<Guid, Settlement> _settlements;
    private readonly Dictionary<Guid, Payment> _payments;
    private readonly Dictionary<Guid, List<Ticket>> _ticketsByPayment;
    private readonly Dictionary<Guid, Donation> _donations;
    private readonly Dictionary<Guid, Performance> _performances;
    private readonly Dictionary<Guid, Performer> _performers;
    private readonly Dictionary<Guid, FnbOrder> _fnbOrders;
    private readonly Dictionary<Guid, string> _showNames;

    private OwnerMoneyTitles(
        Dictionary<Guid, Settlement> settlements, Dictionary<Guid, Payment> payments,
        Dictionary<Guid, List<Ticket>> ticketsByPayment, Dictionary<Guid, Donation> donations,
        Dictionary<Guid, Performance> performances, Dictionary<Guid, Performer> performers,
        Dictionary<Guid, FnbOrder> fnbOrders, Dictionary<Guid, string> showNames)
    {
        _settlements = settlements;
        _payments = payments;
        _ticketsByPayment = ticketsByPayment;
        _donations = donations;
        _performances = performances;
        _performers = performers;
        _fnbOrders = fnbOrders;
        _showNames = showNames;
    }

    private static Guid? ParseId(string? s) => Guid.TryParse(s, out var g) ? g : null;

    /// <param name="settlementIds">Khoản quyết toán cần đặt câu.</param>
    /// <param name="donationIds">Khoản ủng hộ cần đặt câu trực tiếp (bút toán donation trên sổ của chủ phòng trà).</param>
    public static async Task<OwnerMoneyTitles> LoadAsync(
        IUnitOfWork uow, IReadOnlyCollection<Guid> settlementIds, IReadOnlyCollection<Guid> donationIds, CancellationToken ct)
    {
        var settlements = settlementIds.Count == 0 ? [] : await uow.Repository<Settlement, Guid>()
            .FindAsync(s => settlementIds.Contains(s.Id), ct);
        var paymentIds = settlements.Select(s => s.PaymentId).Distinct().ToList();
        var payments = paymentIds.Count == 0 ? [] : await uow.Repository<Payment, Guid>()
            .FindAsync(p => paymentIds.Contains(p.Id), ct);
        var tickets = paymentIds.Count == 0 ? [] : await uow.Repository<Ticket, Guid>()
            .FindAsync(t => t.PaymentId.HasValue && paymentIds.Contains(t.PaymentId.Value), ct);

        // Khoản ủng hộ: trực tiếp, hoặc qua khoản quyết toán của khoản thanh toán ủng hộ.
        var allDonationIds = donationIds
            .Concat(payments.Where(p => p.ReferenceType == "Donation")
                .Select(p => ParseId(p.ReferenceId)).Where(g => g.HasValue).Select(g => g!.Value))
            .Distinct().ToList();
        var donations = allDonationIds.Count == 0 ? [] : await uow.Repository<Donation, Guid>()
            .FindAsync(d => allDonationIds.Contains(d.Id), ct);
        var performanceIds = donations.Select(d => d.PerformanceId).Distinct().ToList();
        var performances = performanceIds.Count == 0 ? [] : await uow.Repository<Performance, Guid>()
            .FindAsync(p => performanceIds.Contains(p.Id), ct);
        var performerIds = performances.Select(p => p.PerformerId).Distinct().ToList();
        var performers = performerIds.Count == 0 ? [] : await uow.Repository<Performer, Guid>()
            .FindAsync(p => performerIds.Contains(p.Id), ct);

        var fnbIds = payments.Where(p => p.ReferenceType == "FnbOrder")
            .Select(p => ParseId(p.ReferenceId)).Where(g => g.HasValue).Select(g => g!.Value).Distinct().ToList();
        var fnbOrders = fnbIds.Count == 0 ? [] : await uow.Repository<FnbOrder, Guid>()
            .FindAsync(o => fnbIds.Contains(o.Id), ct);

        var showIds = tickets.Select(t => t.ShowId)
            .Concat(performances.Select(p => p.LoungeShowId))
            .Concat(fnbOrders.Where(o => o.ShowId.HasValue).Select(o => o.ShowId!.Value))
            .Distinct().ToList();
        var shows = showIds.Count == 0 ? [] : await uow.Repository<LoungeShow, Guid>()
            .FindAsync(s => showIds.Contains(s.Id), ct);

        return new OwnerMoneyTitles(
            settlements.ToDictionary(s => s.Id),
            payments.ToDictionary(p => p.Id),
            tickets.GroupBy(t => t.PaymentId!.Value).ToDictionary(g => g.Key, g => g.ToList()),
            donations.ToDictionary(d => d.Id),
            performances.ToDictionary(p => p.Id),
            performers.ToDictionary(p => p.Id),
            fnbOrders.ToDictionary(o => o.Id),
            shows.ToDictionary(s => s.Id, s => s.Name));
    }

    private string? ShowName(Guid? id) => id is { } g && _showNames.TryGetValue(g, out var n) ? n : null;

    /// <summary>"tiền ủng hộ Thiên Di của Hà Gia Hân — Đêm tình ca Trịnh". Ẩn danh thì không lộ tên người tặng.</summary>
    private string DonationPhrase(Donation d)
    {
        _performances.TryGetValue(d.PerformanceId, out var perf);
        var performer = perf is not null && _performers.TryGetValue(perf.PerformerId, out var p) ? p.Name : "nghệ sĩ";
        var donor = d.IsAnonymous || string.IsNullOrWhiteSpace(d.DisplayName) ? "khán giả ẩn danh" : d.DisplayName;
        var show = ShowName(perf?.LoungeShowId);
        return $"tiền ủng hộ {performer} của {donor}" + (show is null ? "" : $" — {show}");
    }

    private static string TrancheName(SettlementReleaseType t) => t switch
    {
        SettlementReleaseType.Partial70 => "đợt 70%",
        SettlementReleaseType.Final30 => "đợt 30% còn lại",
        _ => "trọn khoản",
    };

    /// <summary>Khoản quyết toán là tiền gì: "Tiền vé đợt 70% — Đêm tình ca Trịnh · 2 vé", "Tiền ủng hộ Thiên Di của … (phòng
    /// trà nhận hộ)", "Tiền đồ uống gọi qua ứng dụng — …".</summary>
    public string Settlement(Guid settlementId)
    {
        if (!_settlements.TryGetValue(settlementId, out var s)) return "Tiền quyết toán";
        var dot = TrancheName(s.ReleaseType);
        if (!_payments.TryGetValue(s.PaymentId, out var pay)) return $"Tiền quyết toán ({dot})";

        if (pay.ReferenceType == "Donation"
            && ParseId(pay.ReferenceId) is { } did && _donations.TryGetValue(did, out var don))
            return $"Tiền ủng hộ: {DonationPhrase(don)} (phòng trà nhận hộ)";

        if (pay.ReferenceType == "FnbOrder"
            && ParseId(pay.ReferenceId) is { } oid && _fnbOrders.TryGetValue(oid, out var order))
        {
            var show = ShowName(order.ShowId);
            return "Tiền đồ uống gọi qua ứng dụng" + (show is null ? "" : $" — {show}");
        }

        if (_ticketsByPayment.TryGetValue(pay.Id, out var ts) && ts.Count > 0)
            return $"Tiền vé {dot} — {ShowName(ts[0].ShowId) ?? "buổi diễn"} · {ts.Count} vé";

        return $"Tiền quyết toán ({dot})";
    }

    /// <param name="outgoing">true = bút toán ghi Nợ trên sổ chủ phòng trà: chủ chuyển tiếp cho nghệ sĩ (chặng 2).</param>
    public string Donation(Guid donationId, bool outgoing)
    {
        if (!_donations.TryGetValue(donationId, out var d)) return "Tiền ủng hộ nghệ sĩ";
        return outgoing ? $"Đã chuyển cho nghệ sĩ: {DonationPhrase(d)}" : $"Nhận hộ {DonationPhrase(d)}";
    }
}
