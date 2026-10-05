using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Application.Settlements;

/// <summary>
/// MLACP-640. Tài khoản nhận tiền của một khoản quyết toán được chọn LÚC GIẢI NGÂN nếu lúc tạo khoản đó phòng trà chưa có.
///
/// <para><b>Trước đây</b> ba nơi tạo khoản quyết toán (vé — <c>ScheduleSettlementHandler</c>, donate —
/// <c>DonationPayouts</c>, đồ uống — <c>FnbSettlements</c>) chụp tài khoản mặc định ngay lúc tạo, phòng trà chưa có thì để
/// null, và chú thích ở cả ba hứa "job hoãn giải ngân cho tới khi có". Nhưng không chỗ nào gán lại: thêm tài khoản, Admin
/// duyệt tài khoản, job chạy lại — khoản cũ vẫn null và bị bỏ qua mãi mãi, trong khi thông báo gửi chủ phòng trà nói "các
/// khoản đang giữ sẽ được chuyển ở lần giải ngân kế tiếp". Đo trên DB cục bộ 05/10/2026: 37 khoản / 8.604.000đ của một
/// phòng trà kẹt như vậy sau khi tài khoản đã được duyệt.</para>
///
/// <para>Chỉ điền chỗ TRỐNG. Khoản đã chụp một tài khoản thì giữ nguyên: đó là nơi chủ phòng trà chỉ định lúc bán, và
/// <see cref="PayeeVerification"/> vẫn chặn nếu tài khoản đó bị sửa mà chưa được xác minh lại.</para>
///
/// <para>Tìm phòng trà theo chủ: mỗi tài khoản chủ chỉ sở hữu MỘT phòng trà (<c>CreateLoungeCommandHandler</c> chặn cái
/// thứ hai), nên chủ của khoản quyết toán xác định đúng một phòng trà. Trần giới hạn: nếu sau này cho một chủ nhiều phòng
/// trà thì phải lưu phòng trà ngay trên khoản quyết toán (cột mới + migration) thay vì suy từ chủ.</para>
/// </summary>
public static class SettlementPayoutAccount
{
    /// <summary>Trả tài khoản của khoản quyết toán; còn trống thì lấy tài khoản mặc định hiện tại của phòng trà và GHI
    /// vào khoản (người gọi lưu cùng lượt). Phòng trà vẫn chưa có tài khoản mặc định thì trả null.</summary>
    public static async Task<Guid?> EnsureAsync(IUnitOfWork uow, Settlement settlement, CancellationToken ct)
    {
        if (settlement.BankAccountId is not null) return settlement.BankAccountId;

        var lounge = (await uow.Repository<MusicLoungeEntity, Guid>()
            .FindAsync(l => l.OwnerId == settlement.OwnerId, ct)).FirstOrDefault();
        if (lounge is null) return null;

        var account = (await uow.Repository<BankAccount, Guid>().FindAsync(
                a => a.OwnerType == BankAccountOwnerType.Lounge && a.OwnerId == lounge.Id && a.IsDefault, ct))
            .FirstOrDefault();
        if (account is null) return null;

        settlement.BankAccountId = account.Id;
        return account.Id;
    }
}
