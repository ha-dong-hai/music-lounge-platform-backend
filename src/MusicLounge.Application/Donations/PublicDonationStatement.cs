using System.Globalization;
using MusicLounge.Application.Common;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Donations.DTOs;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.Donations;

/// <summary>
/// MLACP-365 — sao kê công khai tiền donate của một nghệ sĩ: tiền đang ở đâu, ai đang giữ, đã bao lâu,
/// nghệ sĩ được bao nhiêu và chính nghệ sĩ nói gì.
///
/// <para><b>Trước đây</b> trang công khai chỉ có số tiền, trạng thái và ngày tạo. "PerformerPaid" hiện
/// như một sự thật dù chỉ là lời khai của phòng trà; donate nền tảng đang giữ không hiện; không ai biết
/// một khoản đã nằm ở phòng trà bao lâu.</para>
///
/// <para><b>Căn cứ</b>: Thiện Nguyện (MB) công khai toàn bộ giao dịch theo thời gian thực; GoFundMe cho
/// người donate ẩn tên nhưng số tiền vẫn hiện. Bản sao kê MTTQ năm 2024 làm lộ tên và số tài khoản người
/// chuyển (nguồn báo chí) — nên trang này không bao giờ trả số tài khoản, mã chuyển khoản, mã giao dịch
/// hay ảnh chứng từ; chỉ cho biết có chứng từ được lưu trữ.</para>
///
/// <para>Mốc thời gian lấy từ đúng nguồn các job dùng để nhắc và cảnh cáo
/// (<see cref="DonationPayoutDeadline"/>); phản hồi của nghệ sĩ lấy từ nhật ký bằng chứng
/// (<see cref="DonationEvidence"/>). Trang công khai không có cách tính riêng.</para>
/// </summary>
public static class PublicDonationStatement
{
    public const string PlatformHolding = "PlatformHolding";
    public const string VenueHolding = "VenueHolding";
    public const string VenueReportedPaid = "VenueReportedPaid";
    public const string PerformerConfirmed = "PerformerConfirmed";
    public const string PerformerDisputed = "PerformerDisputed";

    public static async Task<IReadOnlyList<PublicDonationDto>> BuildAsync(
        IUnitOfWork uow, ISystemConfigService config, IReadOnlyList<PublicDonationRow> rows, CancellationToken ct)
    {
        if (rows.Count == 0) return [];

        var now = DateTimeOffset.UtcNow;
        var ids = rows.Select(r => r.Id).ToList();

        var holdDays = await DonationPayoutDeadline.HoldDaysAsync(config, ct);
        var fallbackShareRate = await config.GetDecimalAsync(ConfigKeys.DonationPerformerShareRate, 0.88m, ct);
        var blockedWords = await DonationMessageFilter.LoadBlockedWordsAsync(config, ct);
        var releaseTimes = await DonationPayoutDeadline.PayoutReleaseTimesAsync(uow, ids, ct);

        var referenceIds = ids.Select(id => id.ToString()).ToList();
        var paymentByDonation = (await uow.Repository<Payment, int>().FindAsync(
                p => p.ReferenceType == DonationPayouts.PaymentReferenceType && referenceIds.Contains(p.ReferenceId), ct))
            .ToDictionary(p => int.Parse(p.ReferenceId));

        var events = await uow.Repository<DonationEvent, long>().FindAsync(
            e => ids.Contains(e.DonationId)
                 && (e.EventType == DonationEventType.VenueReportedPaid
                     || e.EventType == DonationEventType.PerformerConfirmedReceipt
                     || e.EventType == DonationEventType.PerformerDisputedReceipt), ct);
        // Số tiền phòng trà khai đã chuyển là số đã ghi vào nhật ký lúc báo — không tính lại theo tỉ lệ.
        var reportedAmountByDonation = events
            .Where(e => e.EventType == DonationEventType.VenueReportedPaid)
            .GroupBy(e => e.DonationId)
            .ToDictionary(g => g.Key, g => g.OrderBy(e => e.Sequence).Last().Amount);
        var responseByDonation = events
            .Where(e => e.EventType != DonationEventType.VenueReportedPaid)
            .GroupBy(e => e.DonationId)
            .ToDictionary(g => g.Key, g => g.OrderBy(e => e.Sequence).Last());

        var nullableIds = ids.Select(id => (int?)id).ToList();
        var askedToConfirm = (await uow.Repository<PerformerConfirmation, int>().FindAsync(
                c => c.Purpose == PerformerConfirmationPurpose.DonationReceipt && nullableIds.Contains(c.DonationId), ct))
            .Select(c => c.DonationId!.Value)
            .ToHashSet();

        return rows.Select(row =>
        {
            var receivedAt = DonationPayoutDeadline.ReceivedAt(
                row.Id, row.PaymentConfirmedAt, row.OwnerAckAt, releaseTimes);
            var dueAt = DonationPayoutDeadline.DueAt(receivedAt, holdDays);
            var reportedPaid = row.Status == DonationStatus.PerformerPaid;
            var overdue = !reportedPaid && now > dueAt;
            var paidLate = reportedPaid && row.OwnerPaidAt > dueAt;
            var asked = askedToConfirm.Contains(row.Id);

            var response = responseByDonation.GetValueOrDefault(row.Id);
            var stage = !reportedPaid
                ? receivedAt is null ? PlatformHolding : VenueHolding
                : response?.EventType switch
                {
                    DonationEventType.PerformerConfirmedReceipt => PerformerConfirmed,
                    DonationEventType.PerformerDisputedReceipt => PerformerDisputed,
                    _ => VenueReportedPaid
                };

            decimal? platformFee = null, tax = null, performerAmount = null, venueRetained = null;
            if (row.IsAmountPublic)
            {
                performerAmount = reportedAmountByDonation.GetValueOrDefault(row.Id)
                    ?? PaymentFeeCalculator.SplitDonationPayout(
                        row.Gross, row.Net, row.PerformerShareRateSnapshot ?? fallbackShareRate).PerformerAmount;
                venueRetained = row.Net - performerAmount;
                if (paymentByDonation.TryGetValue(row.Id, out var payment))
                {
                    platformFee = payment.PlatformFee;
                    tax = payment.TaxWithheld + payment.PersonalIncomeTaxWithheld;
                }
            }

            return new PublicDonationDto(
                row.Id,
                row.ShowName,
                row.VenueName,
                row.ShowDate,
                row.DonorDisplayName,
                row.IsAmountPublic ? row.Gross : null,
                row.Status.ToString(),
                row.CreatedAt,
                row.PaymentConfirmedAt,
                DonationMessageFilter.AllowedMessage(row.Message, row.IsMessagePublic, row.MessageHiddenAt, blockedWords),
                platformFee,
                tax,
                performerAmount,
                venueRetained,
                releaseTimes.GetValueOrDefault(row.Id),
                row.OwnerAckAt,
                row.AutoConfirmed,
                dueAt,
                row.OwnerPaidAt,
                row.HasTransferReceipt,
                overdue,
                paidLate,
                asked,
                response is null
                    ? null
                    : response.EventType == DonationEventType.PerformerConfirmedReceipt ? "Confirmed" : "Disputed",
                response?.OccurredAt,
                stage,
                LabelOf(stage, overdue, asked));
        }).ToList();
    }

    /// <summary>
    /// Nhãn nói đúng điều hệ thống biết: "phòng trà báo đã chuyển" là lời khai của phòng trà, không
    /// phải "nghệ sĩ đã nhận" — chỉ chính nghệ sĩ mới xác nhận được điều đó.
    /// </summary>
    private static string LabelOf(string stage, bool overdue, bool askedToConfirm) => stage switch
    {
        PlatformHolding => "Nền tảng đang giữ — chờ chuyển cho phòng trà",
        VenueHolding => overdue
            ? "Phòng trà đang giữ — đã quá hạn chuyển cho nghệ sĩ"
            : "Phòng trà đang giữ — chờ chuyển cho nghệ sĩ",
        VenueReportedPaid => askedToConfirm
            ? "Phòng trà báo đã chuyển — nghệ sĩ chưa phản hồi"
            : "Phòng trà báo đã chuyển — nghệ sĩ chưa được mời xác nhận",
        PerformerConfirmed => "Nghệ sĩ đã xác nhận nhận được tiền",
        PerformerDisputed => "Nghệ sĩ báo chưa nhận được — đã mở khiếu nại",
        _ => stage
    };

    public static PerformerDonationSummaryDto Summarize(
        int performerId, string performerName, IReadOnlyList<PublicDonationDto> entries, PublicDonationPolicyDto policy)
    {
        decimal ForPerformer(Func<PublicDonationDto, bool> where)
            => entries.Where(where).Sum(e => e.PerformerAmount ?? 0m);

        return new PerformerDonationSummaryDto(
            performerId,
            performerName,
            entries.Count,
            entries.Count(e => e.Gross is null),
            entries.Sum(e => e.Gross ?? 0m),
            ForPerformer(_ => true),
            ForPerformer(e => e.Stage == PlatformHolding),
            ForPerformer(e => e.Stage == VenueHolding),
            ForPerformer(e => e.Overdue),
            entries.Count(e => e.Overdue),
            ForPerformer(e => e.VenueReportedPaidAt is not null),
            ForPerformer(e => e.Stage == PerformerConfirmed),
            ForPerformer(e => e.Stage == PerformerDisputed),
            entries.Count(e => e.PaidLate),
            policy);
    }

    /// <summary>
    /// Chính sách đang áp dụng cho khoản donate mới. Mỗi câu phải đúng với những gì hệ thống thật sự
    /// làm: không có đường hoàn tiền donate nào (ResolveComplaint chỉ hoàn tiền vé), số tiền luôn được
    /// công khai (CreateDonation luôn đặt IsAmountPublic), hạn và mốc cảnh cáo là của các job.
    /// </summary>
    public static async Task<PublicDonationPolicyDto> PolicyAsync(ISystemConfigService config, CancellationToken ct)
    {
        var shareRate = await config.GetDecimalAsync(ConfigKeys.DonationPerformerShareRate, 0.88m, ct);
        var commissionRate = await config.GetDecimalAsync(ConfigKeys.PlatformCommissionRate, 0.05m, ct);
        var holdDays = await DonationPayoutDeadline.HoldDaysAsync(config, ct);
        var warningDays = DonationPayoutDeadline.WarningDays(holdDays);

        return new PublicDonationPolicyDto(
            shareRate,
            commissionRate,
            holdDays,
            warningDays,
            Refundable: false,
            AmountAlwaysPublic: true,
            [
                $"Nghệ sĩ nhận {Percent(shareRate)} số tiền của mỗi khoản donate. Phần còn lại gồm phí nền tảng " +
                $"({Percent(commissionRate)}), thuế khấu trừ tại nguồn theo loại hình kinh doanh của phòng trà, và " +
                "phần phòng trà giữ lại. Tỉ lệ của từng khoản được chốt lúc VNPay xác nhận thanh toán.",
                "Tiền donate về tài khoản của nền tảng; nền tảng chuyển phần của phòng trà ở lần giải ngân kế tiếp.",
                $"Phòng trà phải chuyển phần của nghệ sĩ trong {holdDays} ngày kể từ khi nhận tiền từ nền tảng. " +
                $"Quá {warningDays} ngày mà chưa chuyển, phòng trà bị cảnh cáo.",
                "\"Phòng trà báo đã chuyển\" là thông tin do phòng trà khai. Nghệ sĩ có email được mời tự xác nhận " +
                "đã nhận hoặc báo chưa nhận; báo chưa nhận sẽ tự mở khiếu nại để Admin xử lý.",
                "Số tiền donate luôn được công khai; chọn ẩn danh chỉ ẩn tên người donate.",
                "Donate không được hoàn lại sau khi VNPay xác nhận thanh toán.",
                "Trang này không công khai số tài khoản, mã chuyển khoản hay ảnh chứng từ. Các chứng từ đó được " +
                "lưu trữ để đối chiếu khi có tranh chấp."
            ]);
    }

    private static string Percent(decimal rate) => (rate * 100m).ToString("0.##", CultureInfo.InvariantCulture) + "%";
}
