using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.Settlements;

/// <summary>
/// MLACP-395. Điều kiện để nền tảng chuyển tiền quyết toán cho chủ phòng trà: người nhận đã được xác minh danh tính
/// (CCCD/CMND được Admin duyệt) và tài khoản nhận tiền đã được Admin xác minh.
///
/// <para>Trước task này cả hai chỉ được ghi lại mà không chặn gì: tiền có thể chuyển vào một tài khoản chưa ai kiểm, đứng
/// tên một người chưa ai xác minh — đúng kịch bản chiếm tài khoản rồi đổi số tài khoản nhận tiền. Tham chiếu: Stripe
/// Connect "temporarily pauses charges or payouts if the information isn’t provided or verified"; Luật Thương mại điện
/// tử 2025 (số 122/2025/QH15, hiệu lực 01/7/2026) Điều 17 buộc nền tảng xác thực điện tử danh tính người bán "trước khi
/// cho phép bán hàng" — task này chỉ chặn ở khâu chi trả, chặn ở khâu bán là một quyết định riêng.</para>
///
/// <para>Không đạt thì HOÃN, không huỷ: khoản quyết toán vẫn Scheduled, lần giải ngân sau tự chuyển khi đủ điều kiện —
/// cùng quy tắc hoãn-không-phán-trước mà job giải ngân đã dùng cho khoản chưa có tài khoản nhận.</para>
/// </summary>
public static class PayeeVerification
{
    public static async Task<PayoutBlocker?> BlockerAsync(
        IUnitOfWork uow, IPiiEncryptionService pii, int ownerId, int bankAccountId, CancellationToken ct)
    {
        var owner = await uow.Repository<User, int>().GetByIdAsync(ownerId, ct);
        PayoutBlocker? identity = owner?.CitizenCardReviewStatus switch
        {
            KycReviewStatus.Approved => null,
            KycReviewStatus.Pending => PayoutBlocker.IdentityAwaitingReview,
            KycReviewStatus.Rejected => PayoutBlocker.IdentityRejected,
            _ => PayoutBlocker.IdentityNotSubmitted
        };
        if (identity is not null) return identity;

        var account = await uow.Repository<BankAccount, int>().GetByIdAsync(bankAccountId, ct);
        // MLACP-401. Số tài khoản không còn giải mã được (khoá mã hoá cũ đã mất) thì không chuyển tiền vào đó. Kiểm TRƯỚC bước
        // xác minh: chỉ chủ phòng trà nhập lại được số, nên việc gỡ chặn không được giao cho Admin.
        if (account is not null && pii.TryDecrypt(account.AccountNumber) is null)
            return PayoutBlocker.PayoutAccountUnreadable;
        return account is { IsVerified: true } ? null : PayoutBlocker.PayoutAccountUnverified;
    }

    /// <summary>Việc gỡ chặn nằm ở phía Admin (duyệt hồ sơ, xác minh tài khoản) — khác với việc chủ phòng trà phải nộp
    /// hoặc nộp lại hồ sơ.</summary>
    public static bool WaitsOnAdmin(PayoutBlocker blocker)
        => blocker is PayoutBlocker.IdentityAwaitingReview or PayoutBlocker.PayoutAccountUnverified;
}

/// <summary>Lý do một khoản quyết toán chưa được chuyển cho chủ phòng trà.</summary>
public enum PayoutBlocker
{
    IdentityNotSubmitted,
    IdentityRejected,
    IdentityAwaitingReview,
    PayoutAccountUnverified,

    /// <summary>MLACP-401. Số tài khoản nhận tiền không còn giải mã được — chủ phòng trà cần nhập lại.</summary>
    PayoutAccountUnreadable
}
