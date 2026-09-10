using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Application.FnbOrders;

/// <summary>
/// MLACP-350 — trả cho phòng trà tiền F&amp;B khách đã thanh toán online.
///
/// <para><b>Trước đây</b> tiền F&amp;B online vào tài khoản merchant VNPay của nền tảng, còn IPN ghi
/// sổ cái Có thẳng vào tài khoản User của chủ phòng trà và không tạo <c>Settlement</c> nào. Không có
/// chỉ dẫn chi trả nào đi theo, khoản đó không hiện ở màn thu nhập (<c>GetMyEarnings</c> chỉ đọc
/// Settlement), và nó trái với quy tắc đã ghi ở <c>WriteTicketLedgerHandler</c>: tiền của người bán
/// được <b>giữ ở Platform</b>, chỉ ghi Có tài khoản User khi thật sự giải ngân.</para>
///
/// <para><b>Nay</b> đi đúng khuôn của vé: IPN ghi Có Platform (giữ hộ), và một tranche duy nhất
/// (<see cref="SettlementReleaseType.Full"/>) được lên lịch khi đơn <b>đóng</b> — đã phục vụ và đã trả
/// tiền. Không trả cho phòng trà tiền của món chưa giao: quyết toán vé cũng chỉ tính từ lúc buổi diễn
/// kết thúc. Sau đó giữ thêm <see cref="ConfigKeys.FnbSettlementHoldHours"/> để còn hoàn được nếu món
/// có vấn đề — nền tảng trung gian giữ tiền của người bán một khoảng trước khi chi là cách làm phổ biến
/// (Stripe Connect gọi đó là <c>delay_days</c>). Giải ngân đi qua đúng <c>SettlementReleaseJob</c>.</para>
/// </summary>
public static class FnbSettlements
{
    public const int DefaultHoldHours = 48;

    /// <summary>
    /// Lên lịch chi trả cho một đơn vừa đóng. Không làm gì khi đơn được trả bằng tiền mặt (phòng trà
    /// đã cầm tiền), khi đã lên lịch rồi, hoặc khi khoản thanh toán thuộc kiểu ghi sổ cũ.
    ///
    /// <para>Phải gọi <b>sau</b> khi bút toán mua đã được lưu: chốt "tiền đang giữ ở Platform" đọc từ
    /// cơ sở dữ liệu, và <c>LedgerService</c> chỉ thêm dòng vào change tracker.</para>
    /// </summary>
    public static async Task ScheduleOnCloseAsync(
        IUnitOfWork uow, ISystemConfigService config, FnbOrder order, DateTimeOffset closedAt,
        CancellationToken ct)
    {
        var referenceId = order.Id.ToString();
        var payment = (await uow.Repository<Payment, int>().FindAsync(
                p => p.ReferenceType == FnbOrderPayments.ReferenceType
                     && p.ReferenceId == referenceId
                     && p.Status == PaymentStatus.Confirmed
                     && p.Method == PaymentMethod.Gateway, ct))
            .FirstOrDefault();

        // Tiền mặt: phòng trà cầm tiền từ tay khách, nền tảng chưa bao giờ giữ đồng nào để chi.
        if (payment is null) return;

        // Mỗi thanh toán một khoản quyết toán.
        if (await uow.Repository<Settlement, int>().AnyAsync(s => s.PaymentId == payment.Id, ct))
            return;

        // Thanh toán ghi sổ trước MLACP-350 đã ghi Có thẳng cho chủ phòng trà. Lên lịch chi trả cho nó
        // bây giờ nghĩa là ghi Có họ lần thứ hai, trong khi Platform chưa từng giữ khoản đó.
        var heldByPlatform = await uow.Repository<LedgerEntry, int>().AnyAsync(
            e => e.PaymentId == payment.Id
                 && e.Account.OwnerType == AccountType.Platform
                 && !e.IsDebit, ct);
        if (!heldByPlatform) return;

        var lounge = await uow.Repository<MusicLoungeEntity, int>().GetByIdAsync(order.LoungeId, ct);
        if (lounge is null) return;

        // Không có tài khoản mặc định thì vẫn ghi nhận khoản nợ với đích đến rỗng —
        // SettlementReleaseJob hoãn giải ngân cho tới khi phòng trà đăng ký tài khoản, cùng cách
        // ScheduleSettlementHandler xử lý với vé.
        var bankAccountId = (await uow.Repository<BankAccount, int>().FindAsync(
                a => a.OwnerType == BankAccountOwnerType.Lounge && a.OwnerId == lounge.Id && a.IsDefault, ct))
            .FirstOrDefault()?.Id;

        var holdHours = await config.GetIntAsync(ConfigKeys.FnbSettlementHoldHours, DefaultHoldHours, ct);

        uow.Repository<Settlement, int>().Add(new Settlement
        {
            OwnerId = lounge.OwnerId,
            PaymentId = payment.Id,
            ReleaseType = SettlementReleaseType.Full,
            GrossAmount = payment.GrossAmount,
            PreRateApplied = 1m,
            PostRateApplied = 0m,
            // F&B không thu hoa hồng: NetAmount của thanh toán bằng GrossAmount.
            NetAmount = payment.NetAmount,
            BankAccountId = bankAccountId,
            Status = SettlementStatus.Scheduled,
            ScheduledAt = closedAt.AddHours(holdHours),
            CreatedAt = closedAt
        });
    }
}
