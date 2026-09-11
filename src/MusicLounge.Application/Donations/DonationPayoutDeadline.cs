using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.Donations;

/// <summary>
/// MLACP-362 — một hạn duy nhất cho việc phòng trà chuyển tiền donate cho nghệ sĩ.
///
/// <para><b>Trước đây</b> có năm cách tính: danh sách chờ báo hạn tự xác nhận là +24 giờ trong khi job
/// thật sự chờ <c>donation_hold_days</c>; lịch sử của chủ và điều kiện khiếu nại tính từ lúc VNPay xác
/// nhận (với hai giá trị mặc định khác nhau, 7 và 14); job nhắc/cảnh cáo tính từ lúc chủ bấm "đã nhận".
/// Cái cuối tạo ra một kẽ hở: tự xác nhận đặt lại mốc đó, nên chủ im lặng được cảnh cáo ở ngày 21 còn
/// chủ bấm ngay bị cảnh cáo ở ngày 14 — quy tắc thưởng cho sự chậm trễ.</para>
///
/// <para><b>Nay</b> mọi chỗ tính từ cùng một mốc: lúc phòng trà <b>thật sự nhận tiền</b> — lúc nền tảng
/// giải ngân chặng 1 (MLACP-361). Cú bấm "đã nhận" của chủ là bằng chứng hai chiều, không phải điểm bắt
/// đầu đồng hồ. Hạn = mốc + <c>donation_hold_days</c>; nhắc khi tới hạn; cảnh cáo ở mốc + 2 lần số ngày
/// đó. Mặc định 7/14 ngày — chặt hơn cả luật tip của Mỹ (trả "no later than the regular pay day") lẫn
/// của Anh ("by the end of the month following the month in which the tips are paid").</para>
/// </summary>
public static class DonationPayoutDeadline
{
    public const int DefaultHoldDays = 7;

    public static Task<int> HoldDaysAsync(ISystemConfigService config, CancellationToken ct)
        => config.GetIntAsync(ConfigKeys.DonationHoldDays, DefaultHoldDays, ct);

    /// <summary>
    /// Mã donate → lúc nền tảng đã chuyển tiền cho phòng trà (null: có khoản quyết toán nhưng chưa
    /// chuyển). Không có trong kết quả: donate có từ trước MLACP-361, không có khoản quyết toán nào.
    /// </summary>
    public static async Task<IReadOnlyDictionary<int, DateTimeOffset?>> PayoutReleaseTimesAsync(
        IUnitOfWork uow, IReadOnlyCollection<int> donationIds, CancellationToken ct)
    {
        if (donationIds.Count == 0) return new Dictionary<int, DateTimeOffset?>();

        var referenceIds = donationIds.Select(id => id.ToString()).ToList();
        var payments = await uow.Repository<Payment, int>().FindAsync(
            p => p.ReferenceType == DonationPayouts.PaymentReferenceType && referenceIds.Contains(p.ReferenceId), ct);
        var paymentIds = payments.Select(p => p.Id).ToList();
        var settlementByPayment = (await uow.Repository<Settlement, int>().FindAsync(
                s => paymentIds.Contains(s.PaymentId), ct))
            .GroupBy(s => s.PaymentId)
            .ToDictionary(g => g.Key, g => g.First());

        return payments.ToDictionary(
            p => int.Parse(p.ReferenceId),
            p => settlementByPayment.TryGetValue(p.Id, out var s) && s.Status == SettlementStatus.Released
                ? s.ReleasedAt
                : (DateTimeOffset?)null);
    }

    /// <summary>
    /// Lúc phòng trà thật sự nhận tiền. Donate có khoản quyết toán: lúc giải ngân (null nếu chưa). Donate
    /// cũ đã được ghi Có thẳng cho chủ lúc VNPay xác nhận, nên mốc là lúc đó; dữ liệu rất cũ không có
    /// mốc ấy thì dùng lúc chủ xác nhận — mốc sớm nhất còn lại cho thấy chủ đã có tiền.
    /// </summary>
    public static DateTimeOffset? ReceivedAt(
        int donationId, DateTimeOffset? paymentConfirmedAt, DateTimeOffset? ownerAckAt,
        IReadOnlyDictionary<int, DateTimeOffset?> releaseTimes)
        => releaseTimes.TryGetValue(donationId, out var releasedAt)
            ? releasedAt
            : paymentConfirmedAt ?? ownerAckAt;

    public static DateTimeOffset? ReceivedAt(Donation donation, IReadOnlyDictionary<int, DateTimeOffset?> releaseTimes)
        => ReceivedAt(donation.Id, donation.PaymentConfirmedAt, donation.OwnerAckAt, releaseTimes);

    /// <summary>Hạn chuyển tiền cho nghệ sĩ — cũng là hạn hệ thống tự coi như phòng trà đã nhận.</summary>
    public static DateTimeOffset? DueAt(DateTimeOffset? receivedAt, int holdDays) => receivedAt?.AddDays(holdDays);

    /// <summary>Quá mốc này mà vẫn chưa chuyển thì phòng trà bị cảnh cáo.</summary>
    public static DateTimeOffset? WarningAt(DateTimeOffset? receivedAt, int holdDays) => receivedAt?.AddDays(2 * holdDays);
}
