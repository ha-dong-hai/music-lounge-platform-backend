using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Common.Interfaces.Repositories;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.Users.DTOs;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.Users.Queries.GetOwnerTransactionHistory;

internal sealed class GetOwnerTransactionHistoryQueryHandler
    : IRequestHandler<GetOwnerTransactionHistoryQuery, PaginatedResult<OwnerTransactionDto>>
{
    private readonly ILedgerEntryRepository _repo;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _uow;

    public GetOwnerTransactionHistoryQueryHandler(
        ILedgerEntryRepository repo, ICurrentUserService currentUser, IUnitOfWork uow)
    {
        _repo = repo;
        _currentUser = currentUser;
        _uow = uow;
    }

    public async Task<PaginatedResult<OwnerTransactionDto>> Handle(
        GetOwnerTransactionHistoryQuery request, CancellationToken ct)
    {
        var page = Math.Max(1, request.Page);
        var size = Math.Clamp(request.PageSize, 1, 100);
        var result = await _repo.GetOwnerHistoryAsync(
            _currentUser.UserId, request.Type, request.From, request.To, page, size, ct);

        var items = await WithTitlesAsync(result.Items, ct);
        return new PaginatedResult<OwnerTransactionDto>(items, result.Page, result.PageSize, result.TotalCount);
    }

    // MLACP-655: dịch từng bút toán sang câu chủ phòng trà đọc được. Trước đây trang "Tiền và quyết toán" in thẳng
    // Description nội bộ: "Settlement #01a10b35-… payout", "Donate #01a10ae8-… — chặng 2, trả nghệ sĩ (88% gross)" —
    // chủ phòng trà không biết khoản nào của buổi nào (chủ dự án 05/10/2026: "xem không hiểu gì cả").
    // Chỉ tra cho đúng các dòng của TRANG đang xem (tối đa 100), mỗi bảng một lượt truy vấn — không N+1.
    // Thiếu dữ liệu nguồn (bản ghi đã bị xoá/ẩn danh hoá) thì rơi về câu chung theo loại, không bao giờ in GUID.
    private async Task<List<OwnerTransactionDto>> WithTitlesAsync(
        IReadOnlyList<OwnerTransactionDto> rows, CancellationToken ct)
    {
        Guid? IdOf(OwnerTransactionDto r) => Guid.TryParse(r.ReferenceId, out var g) ? g : null;
        List<Guid> IdsOfType(string type) => rows
            .Where(r => string.Equals(r.Type, type, StringComparison.OrdinalIgnoreCase))
            .Select(IdOf).Where(g => g.HasValue).Select(g => g!.Value).Distinct().ToList();

        // Quyết toán → khoản thanh toán gốc (vé / ủng hộ / đồ uống).
        var settlementIds = IdsOfType(LedgerReferenceTypes.Settlement);
        var settlements = settlementIds.Count == 0 ? [] : await _uow.Repository<Settlement, Guid>()
            .FindAsync(s => settlementIds.Contains(s.Id), ct);
        var paymentIds = settlements.Select(s => s.PaymentId).Distinct().ToList();
        var payments = paymentIds.Count == 0 ? [] : await _uow.Repository<Payment, Guid>()
            .FindAsync(p => paymentIds.Contains(p.Id), ct);

        var tickets = paymentIds.Count == 0 ? [] : await _uow.Repository<Ticket, Guid>()
            .FindAsync(t => t.PaymentId.HasValue && paymentIds.Contains(t.PaymentId.Value), ct);
        var ticketsByPayment = tickets.GroupBy(t => t.PaymentId!.Value).ToDictionary(g => g.Key, g => g.ToList());

        // Khoản ủng hộ: trực tiếp (bút toán donation) hoặc qua quyết toán của khoản thanh toán ủng hộ.
        var donationIds = IdsOfType(LedgerReferenceTypes.Donation)
            .Concat(payments.Where(p => p.ReferenceType == "Donation")
                .Select(p => Guid.TryParse(p.ReferenceId, out var g) ? g : Guid.Empty)
                .Where(g => g != Guid.Empty))
            .Distinct().ToList();
        var donations = donationIds.Count == 0 ? [] : await _uow.Repository<Donation, Guid>()
            .FindAsync(d => donationIds.Contains(d.Id), ct);
        var performanceIds = donations.Select(d => d.PerformanceId).Distinct().ToList();
        var performances = performanceIds.Count == 0 ? [] : await _uow.Repository<Performance, Guid>()
            .FindAsync(p => performanceIds.Contains(p.Id), ct);
        var performerIds = performances.Select(p => p.PerformerId).Distinct().ToList();
        var performers = performerIds.Count == 0 ? [] : await _uow.Repository<Performer, Guid>()
            .FindAsync(p => performerIds.Contains(p.Id), ct);

        var fnbIds = payments.Where(p => p.ReferenceType == "FnbOrder")
            .Select(p => Guid.TryParse(p.ReferenceId, out var g) ? g : Guid.Empty)
            .Where(g => g != Guid.Empty).Distinct().ToList();
        var fnbOrders = fnbIds.Count == 0 ? [] : await _uow.Repository<FnbOrder, Guid>()
            .FindAsync(o => fnbIds.Contains(o.Id), ct);

        var showIds = tickets.Select(t => t.ShowId)
            .Concat(performances.Select(p => p.LoungeShowId))
            .Concat(fnbOrders.Where(o => o.ShowId.HasValue).Select(o => o.ShowId!.Value))
            .Distinct().ToList();
        var shows = showIds.Count == 0 ? [] : await _uow.Repository<LoungeShow, Guid>()
            .FindAsync(s => showIds.Contains(s.Id), ct);

        var settlementById = settlements.ToDictionary(s => s.Id);
        var paymentById = payments.ToDictionary(p => p.Id);
        var donationById = donations.ToDictionary(d => d.Id);
        var performanceById = performances.ToDictionary(p => p.Id);
        var performerById = performers.ToDictionary(p => p.Id);
        var fnbById = fnbOrders.ToDictionary(o => o.Id);
        var showNameById = shows.ToDictionary(s => s.Id, s => s.Name);

        string? ShowName(Guid? id) => id is { } g && showNameById.TryGetValue(g, out var n) ? n : null;

        // "ủng hộ Thiên Di của Hà Gia Hân — Đêm tình ca Trịnh". Ẩn danh thì không lộ tên người tặng.
        string DonationPhrase(Donation d)
        {
            performanceById.TryGetValue(d.PerformanceId, out var perf);
            var performer = perf is not null && performerById.TryGetValue(perf.PerformerId, out var p) ? p.Name : "nghệ sĩ";
            var donor = d.IsAnonymous || string.IsNullOrWhiteSpace(d.DisplayName) ? "khán giả ẩn danh" : d.DisplayName;
            var show = ShowName(perf?.LoungeShowId);
            return $"tiền ủng hộ {performer} của {donor}" + (show is null ? "" : $" — {show}");
        }

        string SettlementTitle(Settlement s)
        {
            var dot = s.ReleaseType switch
            {
                SettlementReleaseType.Partial70 => "đợt 70%",
                SettlementReleaseType.Final30 => "đợt 30% còn lại",
                _ => "trọn khoản",
            };
            if (!paymentById.TryGetValue(s.PaymentId, out var pay))
                return $"Nền tảng chuyển tiền quyết toán ({dot})";

            if (pay.ReferenceType == "Donation"
                && Guid.TryParse(pay.ReferenceId, out var did) && donationById.TryGetValue(did, out var don))
                return $"Nền tảng chuyển {DonationPhrase(don)} (phòng trà nhận hộ)";

            if (pay.ReferenceType == "FnbOrder"
                && Guid.TryParse(pay.ReferenceId, out var oid) && fnbById.TryGetValue(oid, out var order))
            {
                var show = ShowName(order.ShowId);
                return "Tiền đồ uống gọi qua ứng dụng" + (show is null ? "" : $" — {show}");
            }

            if (ticketsByPayment.TryGetValue(pay.Id, out var ts) && ts.Count > 0)
            {
                var show = ShowName(ts[0].ShowId);
                return $"Tiền vé {dot} — {show ?? "buổi diễn"} · {ts.Count} vé";
            }
            return $"Nền tảng chuyển tiền quyết toán ({dot})";
        }

        string TitleOf(OwnerTransactionDto r)
        {
            var id = IdOf(r);
            switch (r.Type.ToLowerInvariant())
            {
                case LedgerReferenceTypes.Settlement:
                    return id is { } sid && settlementById.TryGetValue(sid, out var s)
                        ? SettlementTitle(s) : "Nền tảng chuyển tiền quyết toán";
                case LedgerReferenceTypes.Donation:
                    if (id is { } did && donationById.TryGetValue(did, out var d))
                        // Bút toán ghi Nợ của donation trên tài khoản chủ phòng trà = chặng 2: chủ chuyển cho nghệ sĩ.
                        return r.Amount < 0
                            ? $"Đã chuyển cho nghệ sĩ: {DonationPhrase(d)}"
                            : $"Nhận hộ {DonationPhrase(d)}";
                    return "Tiền ủng hộ nghệ sĩ";
                case LedgerReferenceTypes.Refund:
                    return "Thu hồi tiền vé đã hoàn cho khách";
                case LedgerReferenceTypes.FnbOrder:
                    return "Tiền đồ uống";
                case LedgerReferenceTypes.Payment:
                    return "Tiền vé";
                case LedgerReferenceTypes.Subscription:
                    return "Gói dịch vụ";
                default:
                    return "Giao dịch khác";
            }
        }

        return rows.Select(r => r with { Title = TitleOf(r) }).ToList();
    }
}
