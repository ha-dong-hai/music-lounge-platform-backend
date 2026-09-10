using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.Common;

/// <summary>
/// Bút toán chi trả một tranche quyết toán.
///
/// <para>Tách ra ở MLACP-335 vì từ nay có <b>hai</b> nơi giải ngân: job tự động, và Admin duyệt tay
/// một khoản đã bị park <c>PendingReview</c>. Sổ cái chỉ ghi thêm chứ không sửa được, nên hai nơi
/// ghi lệch nhau là một sai lệch không gỡ được bằng cách sửa dòng cũ — phải viết một bút toán đảo.
/// Giữ đúng một định nghĩa là cách rẻ nhất để chuyện đó không xảy ra.</para>
/// </summary>
public static class SettlementPayout
{
    /// <summary>
    /// Tiền rời tài khoản Platform (đang giữ hộ từ lúc khách thanh toán) sang tài khoản của chủ
    /// phòng trà. Nợ Platform, có User — hai vế bằng nhau đúng <c>NetAmount</c>.
    /// </summary>
    public static LedgerLine[] JournalLines(Settlement settlement) =>
    [
        new(AccountType.Platform, null, settlement.NetAmount, IsDebit: true,
            Description: $"Settlement #{settlement.Id} payout"),
        new(AccountType.User, settlement.OwnerId, settlement.NetAmount, IsDebit: false,
            Description: $"Settlement #{settlement.Id} payout")
    ];
}
