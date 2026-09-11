using MusicLounge.Application.Common;
using MusicLounge.Application.Donations;
using MusicLounge.Application.FnbOrders;
using MusicLounge.Application.Analytics.DTOs;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Application.Analytics.Common;

internal sealed class OwnerRevenueReportBuilder : IOwnerRevenueReportBuilder
{
    // Same VN-local (UTC+7) convention as GetOwnerAnalyticsQueryHandler's RevenueTrend — grouping
    // directly on a UTC-stored CreatedAt.Month skews the month a transaction lands in around every
    // month boundary for a VN-based owner/audience.
    private static readonly TimeSpan VnOffset = TimeSpan.FromHours(7);

    private readonly IUnitOfWork _uow;
    private readonly ISystemConfigService _config;

    public OwnerRevenueReportBuilder(IUnitOfWork uow, ISystemConfigService config)
    {
        _uow = uow;
        _config = config;
    }

    public async Task<OwnerRevenueReportDto> BuildAsync(
        int loungeId, DateTimeOffset? from, DateTimeOffset? to, CancellationToken ct = default)
    {
        var shows = await _uow.Repository<LoungeShow, int>()
            .FindAsync(s => s.LoungeId == loungeId, ct);
        var showIds = shows.Select(s => s.Id).ToHashSet();
        var showById = shows.ToDictionary(s => s.Id);

        bool InRange(DateTimeOffset d) =>
            (!from.HasValue || d >= from.Value) &&
            (!to.HasValue || d <= to.Value);

        // ---- Tickets ----
        var allTickets = await _uow.Repository<Ticket, Guid>()
            .FindAsync(t => showIds.Contains(t.ShowId) && t.Status == TicketStatus.Confirmed, ct);
        var tickets = allTickets.Where(t => InRange(t.CreatedAt)).ToList();

        var priceIds = tickets.Select(t => t.PriceId).Distinct().ToList();
        var prices = await _uow.Repository<TicketPrice, int>().FindAsync(p => priceIds.Contains(p.Id), ct);
        var priceById = prices.ToDictionary(p => p.Id);
        decimal TicketAmount(Ticket t) => priceById.TryGetValue(t.PriceId, out var p) ? p.Price : 0m;

        // ---- F&B (chỉ đơn đã thanh toán) ----
        // MLACP-349: "da thanh toan" khong con dong nghia voi buoc cuoi cua bep. Don tra truoc qua
        // VNPay la tien that tu luc IPN xac nhan, du bep chua phuc vu xong — dem theo Status == Paid
        // se bo sot no. Xem FnbOrderPayments.
        var loungeFnbOrders = await _uow.Repository<FnbOrder, int>()
            .FindAsync(o => o.LoungeId == loungeId && o.Status != FnbOrderStatus.Cancelled, ct);
        var paidFnbOrderIds = await FnbOrderPayments.ConfirmedOrderIdsAsync(
            _uow, loungeFnbOrders.Select(o => o.Id).ToList(), ct);
        var allFnbOrders = loungeFnbOrders
            .Where(o => FnbOrderPayments.IsPaid(o, paidFnbOrderIds.Contains(o.Id)))
            .ToList();
        var fnbOrders = allFnbOrders.Where(o => InRange(o.CreatedAt)).ToList();

        // ---- Donate (đã thu tiền qua VNPay, bất kể đã chuyển cho nghệ sĩ hay chưa) ----
        // MLACP-359: trước đây cả Gross được cộng vào doanh thu của phòng trà. Phần nghệ sĩ được
        // nhận là tiền phòng trà thu hộ và phải chuyển đi — theo VAS 14, khoản thu hộ bên thứ ba
        // không phải doanh thu. Chỉ phần còn lại (Gross − phần nghệ sĩ) là doanh thu của phòng trà.
        // Phần nghệ sĩ tính đúng như ConfirmDonationPaid tính khi ghi sổ chặng 2: cùng hàm, cùng tỉ
        // lệ chốt lúc VNPay xác nhận, cùng tỉ lệ dự phòng cho donate có từ trước khi có cột chốt.
        var fallbackPerformerShareRate = await _config.GetDecimalAsync(
            ConfigKeys.DonationPerformerShareRate, 0.88m, ct);
        decimal ForPerformer(Donation d) => PaymentFeeCalculator.SplitDonationPayout(
            d.Gross, d.Net, d.PerformerShareRateSnapshot ?? fallbackPerformerShareRate).PerformerAmount;
        decimal OwnerShare(Donation d) => d.Gross - ForPerformer(d);

        var performances = await _uow.Repository<Performance, int>()
            .FindAsync(p => showIds.Contains(p.LoungeShowId), ct);
        var performanceIds = performances.Select(p => p.Id).ToHashSet();
        var showIdByPerformance = performances.ToDictionary(p => p.Id, p => p.LoungeShowId);

        var allDonations = await _uow.Repository<Donation, int>().FindAsync(
            d => performanceIds.Contains(d.PerformanceId) && d.PaymentConfirmedAt != null, ct);
        var donations = allDonations.Where(d => InRange(d.PaymentConfirmedAt!.Value)).ToList();

        // ---- Theo buổi diễn ----
        var ticketsByShow = tickets.ToLookup(t => t.ShowId);
        var fnbByShow = fnbOrders.Where(o => o.ShowId.HasValue).ToLookup(o => o.ShowId!.Value);
        var donationsByShow = donations
            .Where(d => showIdByPerformance.ContainsKey(d.PerformanceId))
            .ToLookup(d => showIdByPerformance[d.PerformanceId]);

        var byEvent = showIds
            .Select(id =>
            {
                var ticketRevenue = ticketsByShow[id].Sum(TicketAmount);
                var fnbRevenue = fnbByShow[id].Sum(o => o.TotalAmount);
                var donationRevenue = donationsByShow[id].Sum(OwnerShare);
                var forPerformers = donationsByShow[id].Sum(ForPerformer);
                var total = ticketRevenue + fnbRevenue + donationRevenue;
                var show = showById[id];
                return new RevenueByEventDto(
                    id, show.Name, show.ScheduledStart, ticketRevenue, fnbRevenue, donationRevenue, total,
                    forPerformers);
            })
            // Buổi diễn chỉ có donate vẫn phải hiện dù phần phòng trà giữ lại bằng 0 — khoản thu hộ
            // cũng là tiền đi qua tay phòng trà, cần đối soát.
            .Where(e => e.TotalRevenue > 0 || e.DonationCollectedForPerformers > 0)
            .OrderByDescending(e => e.ScheduledStart)
            .ToList();

        // ---- Theo tháng (toàn bộ các tháng có giao dịch, không giới hạn 6 tháng gần nhất —
        // khác GetOwnerAnalyticsQueryHandler vì đây là báo cáo đối soát, không phải dashboard) ----
        (int Year, int Month) MonthOf(DateTimeOffset d)
        {
            var vn = d.ToOffset(VnOffset);
            return (vn.Year, vn.Month);
        }

        var months = tickets.Select(t => MonthOf(t.CreatedAt))
            .Concat(fnbOrders.Select(o => MonthOf(o.CreatedAt)))
            .Concat(donations.Select(d => MonthOf(d.PaymentConfirmedAt!.Value)))
            .Distinct()
            .OrderBy(ym => ym.Year).ThenBy(ym => ym.Month)
            .ToList();

        var byMonth = months
            .Select(ym =>
            {
                var ticketRevenue = tickets.Where(t => MonthOf(t.CreatedAt) == ym).Sum(TicketAmount);
                var fnbRevenue = fnbOrders.Where(o => MonthOf(o.CreatedAt) == ym).Sum(o => o.TotalAmount);
                var monthDonations = donations.Where(d => MonthOf(d.PaymentConfirmedAt!.Value) == ym).ToList();
                var donationRevenue = monthDonations.Sum(OwnerShare);
                return new RevenueByMonthDto(
                    ym.Year, ym.Month, ticketRevenue, fnbRevenue, donationRevenue,
                    ticketRevenue + fnbRevenue + donationRevenue,
                    monthDonations.Sum(ForPerformer));
            })
            .ToList();

        var totalTicket = tickets.Sum(TicketAmount);
        var totalFnb = fnbOrders.Sum(o => o.TotalAmount);
        var totalDonation = donations.Sum(OwnerShare);
        var totalForPerformers = donations.Sum(ForPerformer);

        // ---- Quyet toan da nhan + phi nen tang da tra (MLACP-207) ----
        // Settlement.OwnerId la User.Id (co the co nhieu venue) — thu hep dung venue nay qua
        // PaymentId cua chinh cac ve thuoc loungeId (Settlement khong co LoungeId/ShowId truc tiep).
        var lounge = await _uow.Repository<MusicLoungeEntity, int>().GetByIdAsync(loungeId, ct);
        var ticketPaymentIds = allTickets
            .Where(t => t.PaymentId.HasValue)
            .Select(t => t.PaymentId!.Value)
            .ToHashSet();

        // MLACP-350: tien F&B online nay cung di qua Settlement — thieu no o day thi "da nhan quyet
        // toan" cua bao cao nho hon so tien phong tra that su nhan duoc.
        var fnbReferenceIds = loungeFnbOrders.Select(o => o.Id.ToString()).ToList();
        var fnbPaymentIds = fnbReferenceIds.Count == 0
            ? []
            : (await _uow.Repository<Payment, int>().FindAsync(
                    p => p.ReferenceType == FnbOrderPayments.ReferenceType
                         && p.Method == PaymentMethod.Gateway
                         && fnbReferenceIds.Contains(p.ReferenceId), ct))
                .Select(p => p.Id)
                .ToHashSet();
        // MLACP-361: tien donate chang 1 nay cung di qua Settlement — tinh vao "da nhan", va phi nen
        // tang cung thue khau tru tren donate hien o "phi nen tang da tra" (Gross - Net cua tranche).
        var donationReferenceIds = allDonations.Select(d => d.Id.ToString()).ToList();
        var donationPaymentIds = donationReferenceIds.Count == 0
            ? []
            : (await _uow.Repository<Payment, int>().FindAsync(
                    p => p.ReferenceType == DonationPayouts.PaymentReferenceType
                         && donationReferenceIds.Contains(p.ReferenceId), ct))
                .Select(p => p.Id)
                .ToHashSet();
        var settledPaymentIds = ticketPaymentIds.Concat(fnbPaymentIds).Concat(donationPaymentIds).ToHashSet();

        var totalSettlementReceived = 0m;
        var totalPlatformFeePaid = 0m;
        if (lounge is not null && settledPaymentIds.Count > 0)
        {
            var allSettlements = await _uow.Repository<Settlement, int>().FindAsync(
                s => s.OwnerId == lounge.OwnerId
                    && settledPaymentIds.Contains(s.PaymentId)
                    && s.Status == SettlementStatus.Released, ct);
            var releasedSettlements = allSettlements
                .Where(s => s.ReleasedAt.HasValue && InRange(s.ReleasedAt.Value))
                .ToList();
            totalSettlementReceived = releasedSettlements.Sum(s => s.NetAmount);
            totalPlatformFeePaid = releasedSettlements.Sum(s => s.GrossAmount - s.NetAmount);
        }

        return new OwnerRevenueReportDto(
            TotalTicketRevenue: totalTicket,
            TotalFnbRevenue: totalFnb,
            TotalDonationRevenue: totalDonation,
            TotalDonationCollectedForPerformers: totalForPerformers,
            GrandTotal: totalTicket + totalFnb + totalDonation,
            TotalSettlementReceived: totalSettlementReceived,
            TotalPlatformFeePaid: totalPlatformFeePaid,
            ByEvent: byEvent,
            ByMonth: byMonth);
    }
}
