using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.Donations;

/// <summary>
/// MLACP-361 — chặng 1 của donate: nền tảng chuyển phần của phòng trà từ tài khoản merchant VNPay
/// của mình vào tài khoản ngân hàng của phòng trà.
///
/// <para><b>Trước đây</b> không có gì làm việc đó. IPN ghi sổ Có thẳng vào tài khoản User của chủ
/// phòng trà, không tạo <see cref="Settlement"/>, không cần tài khoản ngân hàng, và
/// <c>SettlementReleaseJob</c> không đụng tới donate. Chủ phòng trà bị yêu cầu "xác nhận đã nhận"
/// một khoản chưa từng được chuyển, rồi phải tự bỏ tiền chuyển 88% cho nghệ sĩ — hoặc bị cảnh cáo.</para>
///
/// <para><b>Nay</b> đi đúng khuôn của vé và F&amp;B (MLACP-350): IPN tạo một <see cref="Payment"/> cho
/// donate và giữ phần của phòng trà ở Platform; một tranche <see cref="SettlementReleaseType.Full"/>
/// được lên lịch giải ngân ở lần chạy kế tiếp của <c>SettlementReleaseJob</c> — "tức thì" theo §6.5,
/// vì donate không phụ thuộc buổi diễn có giao đủ hay không. Phòng trà chưa có tài khoản mặc định thì
/// khoản quyết toán vẫn được ghi, và job hoãn giải ngân cho tới khi có — không mất khoản nào.</para>
///
/// <para>Donate có từ trước thay đổi này đã được ghi Có thẳng cho chủ và không có <see cref="Payment"/>
/// nào; chúng giữ nguyên luồng cũ, vì lên lịch chi trả cho chúng bây giờ là ghi Có chủ lần thứ hai.</para>
/// </summary>
public static class DonationPayouts
{
    public const string PaymentReferenceType = "Donation";

    public static async Task ScheduleAsync(
        IUnitOfWork uow, Payment payment, int ownerId, int loungeId, DateTimeOffset now, CancellationToken ct)
    {
        var bankAccountId = (await uow.Repository<BankAccount, int>().FindAsync(
                a => a.OwnerType == BankAccountOwnerType.Lounge && a.OwnerId == loungeId && a.IsDefault, ct))
            .FirstOrDefault()?.Id;

        uow.Repository<Settlement, int>().Add(new Settlement
        {
            OwnerId = ownerId,
            PaymentId = payment.Id,
            ReleaseType = SettlementReleaseType.Full,
            GrossAmount = payment.GrossAmount,
            PreRateApplied = 1m,
            PostRateApplied = 0m,
            // Phần của phòng trà sau hoa hồng và thuế — chính là khoản đang giữ ở Platform.
            NetAmount = payment.NetAmount,
            BankAccountId = bankAccountId,
            Status = SettlementStatus.Scheduled,
            ScheduledAt = now,
            CreatedAt = now
        });
    }

    /// <param name="HasPayout">False với donate có từ trước MLACP-361 (không có khoản quyết toán).</param>
    /// <param name="ReleasedAt">Lúc nền tảng đã chuyển tiền cho phòng trà; null khi chưa chuyển.</param>
    public sealed record PayoutState(bool HasPayout, DateTimeOffset? ReleasedAt, bool HasBankAccount);

    public static async Task<PayoutState> StateAsync(IUnitOfWork uow, int donationId, CancellationToken ct)
    {
        var referenceId = donationId.ToString();
        var payment = (await uow.Repository<Payment, int>().FindAsync(
                p => p.ReferenceType == PaymentReferenceType && p.ReferenceId == referenceId, ct))
            .FirstOrDefault();
        if (payment is null) return new PayoutState(false, null, false);

        var settlement = (await uow.Repository<Settlement, int>().FindAsync(
                s => s.PaymentId == payment.Id, ct))
            .FirstOrDefault();

        // Có Payment mà không có quyết toán thì nền tảng đang giữ tiền mà không có lịch chi trả —
        // coi như chưa chuyển, không để phòng trà xác nhận một khoản chưa về.
        return new PayoutState(
            HasPayout: true,
            ReleasedAt: settlement?.Status == SettlementStatus.Released ? settlement.ReleasedAt : null,
            HasBankAccount: settlement?.BankAccountId is not null);
    }
}
