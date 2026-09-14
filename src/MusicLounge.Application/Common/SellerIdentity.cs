using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Application.Common;

/// <summary>
/// MLACP-397. Người bán trên nền tảng — chủ phòng trà — đã được phép bán hay chưa, xét theo danh tính.
///
/// <para>Luật Thương mại điện tử 2025 (122/2025/QH15) Điều 17 khoản 1 điểm c: nền tảng thương mại điện tử trung gian
/// "thực hiện việc xác thực điện tử danh tính ... trước khi cho phép bán hàng". MLACP-395 mới chặn ở khâu chi trả.</para>
///
/// <para>Hệ thống chạy sandbox nên không nối được xác thực điện tử thật (VNeID); bước Admin duyệt CCCD/CMND là bản mô
/// phỏng. Cổng chặn chỉ đọc trạng thái "đã duyệt", nên khi có xác thực thật chỉ cần ghi vào đúng trạng thái đó.</para>
///
/// <para>Chốt ở những lúc nền tảng CHO PHÉP bán — nộp duyệt buổi diễn, tạo đơn F&amp;B, thanh toán đơn F&amp;B — không
/// chốt ở từng lần bán vé của buổi đã đăng: chủ phòng trà nộp lại CCCD giữa lúc buổi diễn đang chạy thì quầy vé tối đó
/// vẫn phải bán được.</para>
/// </summary>
public static class SellerIdentity
{
    public static async Task<KycReviewStatus?> StatusOfAsync(IUnitOfWork uow, int ownerId, CancellationToken ct)
        => (await uow.Repository<User, int>().GetByIdAsync(ownerId, ct))?.CitizenCardReviewStatus;

    /// <summary>Trạng thái danh tính của chủ phòng trà. Null khi không tìm thấy phòng trà — nơi gọi coi là chưa xác minh.</summary>
    public static async Task<KycReviewStatus?> StatusOfVenueSellerAsync(IUnitOfWork uow, int loungeId, CancellationToken ct)
    {
        var lounge = await uow.Repository<MusicLoungeEntity, int>().GetByIdAsync(loungeId, ct);
        return lounge is null ? null : await StatusOfAsync(uow, lounge.OwnerId, ct);
    }

    public static bool IsVerified(KycReviewStatus? status) => status == KycReviewStatus.Approved;

    /// <summary>
    /// Lý do chưa được bán, viết cho chủ phòng trà và nhân viên. Người mua nhận
    /// <see cref="VenueLifecycle.TradingPausedForBuyers"/> — chuyện xác minh là giữa chủ phòng trà với nền tảng.
    /// </summary>
    public static string ExplainForSeller(KycReviewStatus? status) => status switch
    {
        KycReviewStatus.Pending =>
            "Hồ sơ CCCD/CMND của chủ phòng trà đang chờ Admin xác minh.",
        KycReviewStatus.Rejected =>
            "Hồ sơ CCCD/CMND của chủ phòng trà đã bị từ chối — xem lý do trong thông báo và nộp lại.",
        _ =>
            "Chủ phòng trà chưa nộp CCCD/CMND để xác minh danh tính người bán."
    };
}
