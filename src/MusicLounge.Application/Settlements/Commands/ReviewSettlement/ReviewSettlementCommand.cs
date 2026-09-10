using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Settlements.Commands.ReviewSettlement;

/// <summary>
/// Admin quyết một khoản quyết toán đang bị giữ ở <c>PendingReview</c>.
/// </summary>
/// <param name="Decision">
/// <c>"Release"</c> — đã kiểm chứng, chi trả cho phòng trà.
/// <c>"Withhold"</c> — buổi diễn không giao đủ thứ đã bán, không chi tranche này.
/// </param>
/// <param name="Note">Lý do, bắt buộc. Đây là quyết định về tiền nên phải để lại dấu vết.</param>
public sealed record ReviewSettlementCommand(
    int SettlementId,
    string Decision,
    string Note) : ICommand;
